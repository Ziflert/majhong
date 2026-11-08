using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mahjong.Controls;
using Mahjong.Net;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the networked table (docs/multiplayer-draft.md, M1 and M2): the messages of the wire,
    /// the framing that turns a stream back into messages, the projection read back from its text, the
    /// server clock that closes a window nobody answered, a move that comes too late, and a seat that
    /// goes away and comes back.
    ///
    /// Everything here plays through <see cref="LocalTable"/>, that is, through the links a socket would
    /// replace: no test reaches into the state of another seat, because there is no way to.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void WireGroups(SelfTestReport report)
        {
            Run(report, "wire: a message is a line that survives its frame", () => MessageFrames(report));
            Run(report, "wire: a stream is cut into messages, not bytes", () => FrameStream(report));
            Run(report, "wire: the projection reads back as it was written", () => ProjectionRoundTrip(report));
            Run(report, "net: the table clock runs on the server", () => ServerClock(report));
            Run(report, "net: a move that comes too late is refused", () => LateMoveRefused(report));
            Run(report, "net: a seat that goes away and comes back is caught up", () => ReconnectedSeat(report));
            Run(report, "net: four seats play a hand through the links", () => HandOverLinks(report));
            Run(report, "net: a full table refuses the fifth seat", () => TableRefusesOverflow(report));
        }

        // ---------------------------------------------------------------- messages

        private static void MessageFrames(SelfTestReport report)
        {
            var samples = new List<WireMessage>
            {
                WireMessage.Hello("anna"),
                WireMessage.Welcome(2, Wind.West, 1, 4242, "classic144", 17, 63),
                WireMessage.ViewOf(3, 12, "table|3|E|E|AwaitingDiscard|3|0|83\nhand|5c#17"),
                WireMessage.ClockOf(new SeatClock(1, ClockWindow.Claim, TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(1)),
                WireMessage.ClockOf(new SeatClock(1, ClockWindow.Claim, null), TimeSpan.Zero),
                WireMessage.EventOf(4, TableEvent.Meld(2, MeldKind.Pung)),
                WireMessage.ActOf(9, "discard|17|-"),
                WireMessage.AcceptedOf(9, 44),
                WireMessage.RejectedOf(9, "the hand is over"),
                WireMessage.ResumeFrom(31),
                WireMessage.ResumedFrom(31, 36, 44),
                WireMessage.Notice("P1 ran out of time: pass|-|-"),
                WireMessage.SessionOf(new SessionSnapshot(3, 16, Wind.South, 2,
                    TimeSpan.FromMinutes(150), TimeSpan.FromMinutes(147), false, new[] { 12, -4, 8, 0 })),
                WireMessage.StandingsOf(new[]
                {
                    new Standing(1, 0, 12, 4, false, false),
                    new Standing(2, 2, 8, 2, false, false),
                    new Standing(3, 1, -4, 1, true, false),
                    new Standing(3, 3, 0, 1, true, true)
                }),
                WireMessage.Refuse("the table is full")
            };

            foreach (var sample in samples)
            {
                var wire = sample.Encode();
                report.Check(wire.IndexOf('\n') < 0, $"{sample.Kind} fits on one line");
                report.Check(WireMessage.TryParse(wire, out var back, out var error), $"{wire} parses: {error}");
                report.Equal(sample.Kind, back.Kind, "the kind survives the wire");
                report.Equal(wire, back.Encode(), "a message that is read back encodes the same way");
            }

            // An act message carries the move line of the log, separator included: the escaping keeps
            // the fields apart even though the move is nothing but separators.
            var act = WireMessage.Parse(WireMessage.ActOf(7, "chow|17|21,22").Encode());
            report.Check(WireMessage.TryReadAct(act, out var request, out var move, out _), "the act is readable");
            report.Equal(7, request, "the request id comes back");
            report.Equal("chow|17|21,22", move, "the move comes back whole");
            report.Equal("chow", ActionCodec.TryDecode(move, out var intent, out _) ? ActionCodec.KindCode(intent.Kind) : "?",
                "the move read back is still a move");

            // A reason is a sentence: it may hold the separator, the escape character and a line break.
            var nasty = "a|b %c\nd\r";
            var rejected = WireMessage.Parse(WireMessage.RejectedOf(1, nasty).Encode());
            report.Equal(nasty, rejected.Field(1), "a field with separators and line breaks survives");
            report.Equal("%25", WireMessage.Escape("%"), "the escape character escapes itself");
            report.Equal("100%", WireMessage.Unescape(WireMessage.Escape("100%")), "and comes back");

            report.Check(!WireMessage.TryParse("ерунда|1", out _, out _), "a line that is not a message is refused");
            report.Check(!WireMessage.TryParse("", out _, out _), "an empty line is refused");
            report.Check(!WireMessage.TryParse(null, out _, out _), "nothing at all is refused");

            // Cyrillic is fine: a player name and a refusal travel as text, not as bytes of a code page.
            var name = WireMessage.Parse(WireMessage.Hello("Дмитрий").Encode());
            report.Equal("Дмитрий", name.Field(0), "a name in russian survives");
        }

        // ---------------------------------------------------------------- framing

        private static void FrameStream(SelfTestReport report)
        {
            var first = WireFrame.Pack("hello|anna");
            var second = WireFrame.Pack("clock|1|Claim|3000");
            report.Equal("10\nhello|anna", Encoding.UTF8.GetString(first),
                "a frame starts with the byte count of the payload");
            var cyrillic = WireFrame.Pack("привет");
            report.Equal("12\n", Encoding.UTF8.GetString(cyrillic, 0, 3),
                "the frame counts UTF-8 bytes, not characters");
            report.Equal("привет", new FrameReader().Push(cyrillic)[0], "and the payload comes back in one piece");

            // One frame, pushed in two halves.
            var reader = new FrameReader();
            var cut = first.Length / 2;
            report.Equal(0, reader.Push(first, 0, cut).Count, "half a frame is not a message");
            report.Check(reader.Pending > 0, "the unfinished half is kept");
            var whole = reader.Push(first, cut, first.Length - cut);
            report.Equal(1, whole.Count, "the second half completes the message");
            report.Equal("hello|anna", whole[0], "the payload comes out whole");
            report.Equal(0, reader.Pending, "nothing is left over");

            // Two frames in one chunk.
            var both = first.Concat(second).ToArray();
            var pair = new FrameReader().Push(both);
            report.Equal(2, pair.Count, "two frames in one read give two messages");
            report.Equal("clock|1|Claim|3000", pair[1], "and they stay in order");

            // A whole frame, a byte at a time.
            var drip = new FrameReader();
            var seen = new List<string>();
            foreach (var b in second)
            {
                foreach (var payload in drip.Push(new[] { b })) seen.Add(payload);
            }
            report.Equal(1, seen.Count, "a frame that arrives byte by byte is still one message");
            report.Equal("clock|1|Claim|3000", seen[0], "and it is the message that was sent");

            // A payload that holds line breaks is still one message.
            var multiline = new FrameReader().Push(WireFrame.Pack("table|0\nhand|5c#17\n"));
            report.Equal(1, multiline.Count, "line breaks inside a payload do not split it");

            // A stream that makes no sense cannot be resynchronised, so it is an error.
            report.Check(Throws(() => new FrameReader().Push(Encoding.UTF8.GetBytes("abc\npayload"))),
                "a header that is not a number is refused");
            report.Check(Throws(() => new FrameReader().Push(Encoding.UTF8.GetBytes("99999999\n"))),
                "a frame that claims a megabyte and more is refused");
            report.Check(!Throws(() => new FrameReader().Push(Encoding.UTF8.GetBytes("12\nshort"))),
                "an incomplete frame is not an error, just not finished yet");
        }

        private static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (FormatException)
            {
                return true;
            }
        }

        // ------------------------------------------------------------- projection

        private static void ProjectionRoundTrip(SelfTestReport report)
        {
            // A hand with declared combinations in it: the round trip has to carry melds, flowers,
            // discards and offers, not just the own hand.
            var table = new InProcessTable(0, 606060, Wind.East, Bots.BotLevel.Strategic, 0, 1, 2, 3);
            table.PlayOut();

            for (var seat = 0; seat < 4; seat++)
            {
                var view = table.View(seat);
                var text = view.Describe();
                var back = SeatView.Read(text);

                report.Equal(text, back.Describe(), $"seat {seat}: a projection written and read is the same projection");
                report.Equal(view.Seat, back.Seat, "the seat survives");
                report.Equal(view.SeatWind, back.SeatWind, "the seat wind survives");
                report.Equal(view.RoundWind, back.RoundWind, "the round wind survives");
                report.Equal(view.Phase, back.Phase, "the phase survives");
                report.Equal(view.CurrentPlayer, back.CurrentPlayer, "the turn survives");
                report.Equal(view.Dealer, back.Dealer, "the dealer survives");
                report.Equal(view.WallRemaining, back.WallRemaining, "the wall count survives");
                report.Equal(view.ConcealedNotation, back.ConcealedNotation, "the own hand survives, tile by tile");
                report.Equal(view.Offers.Count, back.Offers.Count, "every offer survives");
                report.Equal(view.Players.Count, back.Players.Count, "all four players survive");

                for (var player = 0; player < view.Players.Count; player++)
                {
                    report.Equal(view.Players[player].ConcealedCount, back.Players[player].ConcealedCount,
                        $"seat {seat}: the count of seat {player} survives");
                    report.Equal(view.Players[player].Melds.Count, back.Players[player].Melds.Count,
                        $"seat {seat}: the melds of seat {player} survive");
                    report.Equal(view.Players[player].Discards.Count, back.Players[player].Discards.Count,
                        $"seat {seat}: the discards of seat {player} survive");
                    report.Equal(view.Players[player].Flowers.Count, back.Players[player].Flowers.Count,
                        $"seat {seat}: the flowers of seat {player} survive");
                    if (view.Players[player].Melds.Count > 0)
                        report.Equal(TileNotation.Format(view.Players[player].Melds[0].Tiles),
                            TileNotation.Format(back.Players[player].Melds[0].Tiles),
                            $"seat {seat}: the tiles of the first meld of seat {player} survive");
                }

                // The text is a projection and nothing more: the hidden hands of the other seats are
                // not in it, neither as tiles nor as a second hand line.
                var visible = TileIdsInText(text);
                for (var other = 0; other < 4; other++)
                {
                    if (other == seat) continue;
                    foreach (var hidden in table.State.Players[other].Concealed)
                        report.Check(!visible.Contains(hidden.InstanceId),
                            $"seat {seat} must not see {TileNotation.Format(hidden)} of seat {other}");
                }
            }

            // A projection read back knows which move each offer stands for.
            var opener = new InProcessTable(0, 909090, Wind.East, Bots.BotLevel.Strategic, 1, 2, 3);
            opener.Pump();
            var opening = SeatView.Read(opener.View(0).Describe());
            var discard = opening.Offers.First(offer => offer.Kind == ActionKind.Discard);
            report.Check(ActionCodec.Resolve(opening.Offers, ActionCodec.IntentOf(discard)) != null,
                "a move picked from the projection read back is offered by it");
            report.Check(opening.Offers.All(offer => offer.PlayerIndex == 0), "the offers belong to the seat");

            // A projection with a finished hand carries the winner's two numbers.
            var ended = SeatView.Read(table.View(0).Describe());
            if (table.State.Result != null)
            {
                report.Check(ended.Winner != null, "the winner reaches the client");
                report.Equal(table.State.Result.Score.TotalPoints, ended.Winner!.TotalPoints, "and the score with him");
            }
            else
            {
                report.Check(ended.EndedInDraw, "a drawn hand says so");
            }
        }

        // ----------------------------------------------------------------- clocks

        private static void ServerClock(SelfTestReport report)
        {
            var table = new LocalTable(0, 9091, Wind.East, RuleVariant.Sports, ClockLimits.Rules);
            var seats = Enumerable.Range(0, 4).Select(index => table.Connect($"p{index}")).ToList();
            table.Pump();

            report.Equal(GamePhase.AwaitingDiscard, table.Server.State.Phase, "the deal is ready for the first discard");
            var dealer = table.Seat(0)!;
            report.Equal(ClockWindow.Arrange, dealer.ClockWindow, "the dealer has the twenty seconds of 3.5.7.6 to arrange");
            report.Check(dealer.ClockLeft > TimeSpan.FromSeconds(19) && dealer.ClockLeft <= TimeSpan.FromSeconds(20),
                $"the window is twenty seconds, not {dealer.ClockLeft}");
            report.Check(!table.Seat(1)!.OnClock, "a seat that is not on turn is not on the clock");

            report.Equal(0, table.Server.MoveIndex, "thinking does not play a move");
            table.Tick(TimeSpan.FromSeconds(19));
            report.Equal(0, table.Server.MoveIndex, "the window is still open one second before its end");

            table.Tick(TimeSpan.FromSeconds(1));
            report.Check(table.Server.Table.Log.Any(entry => entry.Wire.StartsWith("discard")),
                "a window nobody answered is played by the table");
            report.Check(table.Server.State.Discards.Count == 1, "the tile went to the discards of the dealer");
            report.Equal(GamePhase.AwaitingDiscard, table.Server.State.Phase, "the hand moved on to the next seat");
            report.Check(table.Seat(1)!.OnClock, "and that seat is now the one on the clock");
            report.Check(table.Seat(1)!.Notices.Any(notice => notice.Contains("ran out of time")),
                "the seats are told the move was the clock's, not the player's");
            report.Check(seats.All(seat => seat.Events.Any(entry => entry.Kind == "discard")),
                "every seat saw the discard it has to animate");

            // The claim window is three seconds (3.6.7) and is open to whoever may claim at once.
            var claimers = Enumerable.Range(0, 4).Where(seat => table.Seat(seat)!.OnClock).ToList();
            if (table.Server.State.Phase == GamePhase.AwaitingClaims)
            {
                report.Check(claimers.All(seat => table.Seat(seat)!.ClockWindow == ClockWindow.Claim),
                    "the seats that may claim have the three second window");
                table.Tick(TimeSpan.FromSeconds(3));
                report.Check(table.Server.State.Phase != GamePhase.AwaitingClaims,
                    "silence in a claim window is a pass, so the window closes");
            }

            // A table played without limits never closes a window, and nothing is played for anybody.
            var free = new LocalTable(0, 9091, Wind.East, RuleVariant.Sports, ClockLimits.Unlimited);
            var loose = free.Connect("slow");
            free.Pump();
            free.Tick(TimeSpan.FromMinutes(10));
            report.Equal(0, free.Server.MoveIndex, "a table without limits waits for its player");
            report.Equal(ClockWindow.Arrange, loose.ClockWindow, "the window is open all the same");
            report.Equal(TimeSpan.Zero, loose.ClockLeft, "with no countdown in it");

            // The windows of the whole table are one object: arming a seat twice keeps one window.
            var clock = new TableClock(ClockLimits.Rules);
            clock.Arm(1, ClockWindow.Claim, TimeSpan.FromSeconds(10));
            var armed = clock.Armed;
            report.Equal(1, armed.Count, "one seat, one window");
            report.Equal(TimeSpan.FromSeconds(3), clock.Of(1)!.Remaining(TimeSpan.FromSeconds(10)), "the claim window is three seconds");
            report.Equal(0, clock.RunOut(TimeSpan.FromSeconds(12)).Count, "it has not run out yet");
            report.Equal(1, clock.RunOut(TimeSpan.FromSeconds(13)).Count, "it has by the thirteenth second");
            clock.Disarm(1);
            report.Equal(0, clock.Armed.Count, "a disarmed seat is not on the clock");
        }

        // ------------------------------------------------------------- late moves

        private static void LateMoveRefused(SelfTestReport report)
        {
            var table = new LocalTable(0, 4321);
            var dealer = table.Connect("dealer");
            var south = table.Connect("south");
            table.Pump();

            var move = dealer.Offers.First(offer => offer.Kind == ActionKind.Discard);
            dealer.Play(move);
            table.Pump();
            report.Equal(1, dealer.Accepted, "the offered move is accepted");
            report.Equal(1, table.Server.MoveIndex, "and it is in the log");

            // The same line a second time is not a move any more: the tile is gone.
            dealer.Act(ActionCodec.Encode(move));
            table.Pump();
            report.Equal(1, table.Server.MoveIndex, "a move that is no longer offered changes nothing");
            report.Check(dealer.LastReject.Length > 0, "the refusal carries a reason");
            report.Check(dealer.LastReject.Contains("not offered"), $"and says what is wrong: {dealer.LastReject}");

            // A move that comes after its window has run out is refused as well: the table has already
            // played the window, so the seat is not even on the clock.
            var before = table.Server.MoveIndex;
            table.Tick(TimeSpan.FromSeconds(30));
            report.Check(table.Server.MoveIndex > before, "the silent seat was played for");

            var stale = before;
            var seat = -1;
            for (var index = 0; index < 4; index++)
            {
                var candidate = table.Seat(index);
                if (candidate == null || !candidate.IsMyTurn) continue;
                seat = index;
                break;
            }

            if (seat >= 0)
            {
                var client = table.Seat(seat)!;
                var discard = client.Offers.FirstOrDefault(offer => offer.Kind == ActionKind.Discard);
                if (discard == null) return;
                stale = table.Server.MoveIndex;
                table.Tick(TimeSpan.FromSeconds(30));
                report.Check(table.Server.MoveIndex > stale, "the window ran out first");
                stale = table.Server.MoveIndex;
                client.Play(discard);
                table.Pump();
                report.Equal(stale, table.Server.MoveIndex, "a move stated after its window is not played");
                report.Check(client.Refused > 0, "and the seat is told why");
            }

            // A seat cannot speak for another seat's window.
            var other = table.Seat(1)!;
            var foreignBefore = table.Server.MoveIndex;
            other.Act("pass|-|-");
            south.Act("discard|1|-");
            table.Pump();
            report.Equal(foreignBefore, table.Server.MoveIndex, "a move nobody was offered changes nothing");
        }

        // ---------------------------------------------------------- reconnection

        private static void ReconnectedSeat(SelfTestReport report)
        {
            var table = new LocalTable(0, 20260101);
            table.AddBot(1);
            table.AddBot(2);
            table.AddBot(3);

            var human = table.Connect("anna");
            var seat = human.Seat;
            report.Equal(0, seat, "the first person to sit down takes the dealer seat");

            for (var guard = 0; guard < 80 && !human.IsHandOver; guard++)
            {
                table.Pump();
                if (human.IsHandOver) break;
                if (!human.IsMyTurn && human.Choices.Count == 0) break;
                human.Play(SimpleMove(human));
            }

            report.Check(table.Server.MoveIndex > 10, $"the hand got going ({table.Server.MoveIndex} moves)");
            var before = human.EventsSeen;
            report.Check(before > 0, "the person has seen events to compare");

            // The hand is not over: a table that went to its end would prove nothing about a seat leaving.
            if (table.Server.State.Phase == GamePhase.HandFinished)
            {
                table.Server.StartNextHand(1, 20260102);
                table.Pump();
            }

            // The seat goes away. The table plays on without it — the clock takes over its windows.
            table.Server.Detach(seat);
            table.Pump();
            var movesAfterLeaving = table.Server.MoveIndex;
            table.Tick(TimeSpan.FromSeconds(60));
            report.Check(table.Server.MoveIndex > movesAfterLeaving, "the hand went on without the seat");
            report.Check(table.Seat(seat) == human, "the old client is still the one that holds the seat in the list");

            // It comes back: a fresh link, a resume, and the same projection the table itself holds.
            var back = table.Connect("anna", seat);
            report.Equal(seat, back.Seat, "the seat is the one that was left");
            report.Equal(table.Server.EventCount, back.EventsSeen, "the seat is caught up on the events");
            report.Equal(SeatView.For(table.Server.State, seat).Describe(), back.View!.Describe(),
                "a seat that comes back sees exactly what the table holds, no more");
            report.Equal(table.Server.MoveIndex, back.MoveIndex, "and it knows where the hand stands");

            // A client that was away longer than it can remember asks for everything it has not seen:
            // the resume is answered with the events it missed and the projection itself.
            var (raw, rawEnd) = LoopbackLink.Create(true);
            var rawSeat = table.Server.Attach(rawEnd, "raw");
            report.Check(rawSeat >= 0, "a fifth seat of the same table is free again after the seat left");
            var hello = raw.Receive();
            report.Check(hello.Any(payload => payload.StartsWith("welcome|")), "the table introduces itself at once");
            report.Check(hello.Any(payload => payload.StartsWith("view|")), "and hands the projection of the seat");

            raw.Send(WireMessage.ResumeFrom(0).Encode());
            table.Pump();
            var caughtUp = raw.Receive();
            report.Equal(table.Server.EventCount, caughtUp.Count(payload => payload.StartsWith("event|")),
                "a resume from the very beginning sends every event of the hand");
            report.Check(caughtUp.Any(payload => payload.StartsWith("resumed|")), "and says how far the catch-up went");
            report.Check(caughtUp.Any(payload => payload.StartsWith("view|")), "and gives the projection again");
            report.Check(caughtUp.Any(payload => payload.StartsWith("clock|")), "and the window the seat has now");

            // After the resume the seat plays on like any other: the table does not care that it left.
            if (!back.IsHandOver && (back.IsMyTurn || back.Choices.Count > 0))
            {
                var movesBefore = table.Server.MoveIndex;
                back.Play(SimpleMove(back));
                table.Pump();
                report.Check(table.Server.MoveIndex > movesBefore, "the reconnected seat plays again");
            }

            // A resume asks only for what is missing: what the client already has is not sent twice.
            back.Resume();
            table.Pump();
            report.Equal(table.Server.EventCount, back.EventsSeen, "a resume with nothing missing adds nothing");
            report.Equal(table.Server.MoveIndex, back.MoveIndex, "and keeps the position the client had");
        }

        // ------------------------------------------------------------- whole hand

        private static void HandOverLinks(SelfTestReport report)
        {
            // Fragmented on purpose: every frame arrives in three byte pieces, the way a socket hands
            // them over. A client that survives this is a client a socket cannot break.
            var table = new LocalTable(3, 24680, Wind.East, RuleVariant.Sports, ClockLimits.Rules, fragment: true);
            var seats = Enumerable.Range(0, 4).Select(index => table.Connect($"p{index}")).ToList();
            report.Check(seats.All(client => client.Seat >= 0), "every client got a seat");

            for (var guard = 0; guard < 8000; guard++)
            {
                if (table.Server.State.Phase == GamePhase.HandFinished) break;
                table.Pump();
                var actor = seats.FirstOrDefault(client => !client.IsHandOver && (client.IsMyTurn || client.Choices.Count > 0));
                if (actor == null) break;
                actor.Play(SimpleMove(actor));
            }

            report.Equal(GamePhase.HandFinished, table.Server.State.Phase, "the hand played over the links reaches its end");
            report.Check(table.Server.MoveIndex > 20, $"the hand took {table.Server.MoveIndex} moves");
            report.Equal(1, table.Server.Session.HandsPlayed, "the network session settles the hand once");
            report.Check(seats.All(client => client.Session?.HandsPlayed == 1),
                "every client receives the public session score sheet");
            report.Check(seats.All(client => client.Settlements.Count == 1),
                "every client receives the settlement belonging to the hand");
            report.Check(table.Server.State.Result != null || table.Server.State.EndedInDraw,
                "the hand ends in a win or in a draw");

            foreach (var client in seats)
            {
                report.Equal(0, client.Refused, $"{client.Name} was never refused");
                report.Check(client.View != null, $"{client.Name} has a projection");
                report.Equal(GamePhase.HandFinished, client.View!.Phase, $"{client.Name} knows the hand is over");
                report.Equal(table.Server.EventCount, client.EventsSeen, $"{client.Name} has every event");
                report.Equal(table.Server.MoveIndex, client.MoveIndex, $"{client.Name} is at the last move");
            }

            // Nothing leaked through the links: every client's text mentions only what that seat may know.
            for (var seat = 0; seat < 4; seat++)
            {
                var text = seats[seat].View!.Describe();
                var visible = TileIdsInText(text);
                for (var other = 0; other < 4; other++)
                {
                    if (other == seat) continue;
                    foreach (var hidden in table.Server.State.Players[other].Concealed)
                        report.Check(!visible.Contains(hidden.InstanceId),
                            $"seat {seat} never saw {TileNotation.Format(hidden)} of seat {other}");
                }
            }
        }

        private static void TableRefusesOverflow(SelfTestReport report)
        {
            var table = new LocalTable(0, 1357);
            for (var seat = 0; seat < 4; seat++) table.Connect($"p{seat}");
            table.Pump();

            var fifth = table.Connect("late");
            report.Equal(-1, fifth.Seat, "there is no fifth seat at a table of four");
            report.Equal("the table is full", fifth.Refusal, "and the refusal says so");

            var taken = new LocalTable(0, 1357);
            var first = taken.Connect("first", 2);
            report.Equal(2, first.Seat, "a seat can be asked for by number");
            var same = taken.Connect("second", 2);
            report.Equal(-1, same.Seat, "a seat that is taken cannot be taken twice");
            report.Check(same.Refusal.Contains("2"), "the refusal names the seat");
        }

        /// <summary>
        /// The simplest legal answer a client can give: win if it can, otherwise pass a claim, otherwise
        /// throw the first tile on offer. Enough to drive a whole hand through the wire.
        /// </summary>
        private static LegalAction SimpleMove(TableClient client)
        {
            var offers = client.Offers;
            var win = offers.FirstOrDefault(offer => offer.Kind == ActionKind.Win);
            if (win != null) return win;

            var pass = offers.FirstOrDefault(offer => offer.Kind == ActionKind.Pass);
            if (pass != null) return pass;

            var discard = offers.FirstOrDefault(offer => offer.Kind == ActionKind.Discard);
            return discard ?? offers[0];
        }
    }
}
