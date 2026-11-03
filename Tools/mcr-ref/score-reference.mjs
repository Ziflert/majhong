// Reference MCR scorer: reads a small JSON job, scores it with the independent
// miaoda-game-mcr-mahjong-analysis-core library and prints one JSON line per hand.
//
//   node score-reference.mjs hands.json
//
// Job format: an array of hand descriptors that mirror the golden-hands file, so the
// reference run and the in-engine run score exactly the same inputs.

import { readFileSync } from 'node:fs';
import { analyzeMcrHand } from 'miaoda-game-mcr-mahjong-analysis-core';

const [, , inputPath] = process.argv;
if (!inputPath) {
  console.error('usage: node score-reference.mjs <hands.json>');
  process.exit(1);
}

const doc = JSON.parse(readFileSync(inputPath, 'utf8'));
const hands = Array.isArray(doc) ? doc : doc.hands;

for (const hand of hands) {
  const result = analyzeMcrHand({
    concealedTilesBeforeWin: hand.tiles,
    declaredMelds: hand.melds ?? [],
    winningTile: hand.win,
    roundWind: hand.roundWind ?? 'E',
    seatWind: hand.seatWind ?? 'E',
    event: {
      source: hand.source ?? 'discard',
      lastWallEvent: hand.lastWallEvent ?? false,
      lastCopy: hand.lastCopy ?? false,
      flowerCount: hand.flowerCount ?? 0,
    },
  });

  const out = { id: hand.id, tiles: hand.tiles, win: hand.win };  if (result.ok) {
    out.total = result.analysis.total;
    out.nonFlowerTotal = result.analysis.nonFlowerTotal;
    out.flowerPoints = result.analysis.flowerPoints;
    out.fans = result.analysis.occurrences.map((o) => ({
      code: o.fanCode,
      name: o.name,
      points: o.points,
    }));
    out.exclusions = result.analysis.exclusions.map((e) => ({ name: e.name, by: e.excludedBy.map((b) => b.name) }));
  } else {
    out.failed = result.reason;
    out.message = result.message;
  }
  console.log(JSON.stringify(out));
}
