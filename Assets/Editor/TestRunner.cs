using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mahjong.EditorTools
{
    /// <summary>
    /// Runs the on-screen controls tests (У-6) after the editor finishes compiling.
    /// Used from the command line because `-executeMethod` starts before the assembly cache
    /// has settled: the batch mode above reports "Scripts have compiler errors" even though
    /// the build succeeds.
    /// </summary>
    public static class ControlsTestRunner
    {
        private static int tick;
        private const int ReadyTick = 60;

        [InitializeOnLoadMethod]
        private static void Arm()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            tick++;
            if (tick < ReadyTick) return;

            EditorApplication.update -= Tick;
            try
            {
                var start = DateTime.Now;
                var passed = 0;
                var failed = 0;

                foreach (var method in typeof(Mahjong.PlayTests.ControlsPanelTests).GetMethods())
                {
                    if (!method.Name.StartsWith("Tray")) continue;
                    try
                    {
                        var instance = Activator.CreateInstance(typeof(Mahjong.PlayTests.ControlsPanelTests));
                        method.Invoke(instance, null);
                        passed++;
                        Debug.Log("[controls tests] " + method.Name + ": PASSED");
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Debug.LogError("[controls tests] " + method.Name + ": FAILED - " +
                            (e.InnerException?.Message ?? e.Message));
                    }
                }

                Debug.Log("[controls tests] " + passed + " passed, " + failed + " failed in " + (DateTime.Now - start));
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(failed > 0 ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogError("[controls tests] setup error: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
