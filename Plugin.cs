using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using Unity;
using HarmonyLib;
using HarmonyLib.Tools;
using ULTRAKILL.Cheats;
using ProjectProphet;
using ProjectProphet.Behaviours;
using ProjectProphet.Behaviours.Wares;

namespace Updraft;

internal static class PluginInfo
{
    public const string PLUGIN_GUID = "com.blaixenu.updraft";
    public const string PLUGIN_NAME = "Updraft";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; } = null!;
        
    private void Awake()
    {

        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} loaded! Yippee!!!");
        gameObject.hideFlags = HideFlags.DontSaveInEditor;

        DoPatching();

    }

    private static void DoPatching()
    {
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}

[HarmonyPatch]
public class Patches
{
    private static int remainingDrafts;
    
    private static void UpdraftLogic(Wings wings)
    {
        NewMovement player = wings.mov;
        bool pressedJump = wings.input.Jump.WasPerformedThisFrame;
        
        if (player.gc.onGround)
        {
            remainingDrafts = 1;
            return;
        }
        else
        {
            if (remainingDrafts > 0 && pressedJump)
            {
                remainingDrafts -= 1;

                Vector3 velocityBuffer = player.rb.velocity; // ignore that i probably dont know what buffer means
                velocityBuffer.y = +10;
                player.rb.velocity = velocityBuffer;
            }
        }
    }

    [HarmonyTranspiler,
    HarmonyPatch(typeof(Wings), nameof(Wings.FixedUpdate))]
    private static IEnumerable<CodeInstruction> UpdraftTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        codeMatcher.Start()
                   .MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Wings), nameof(Wings.charge))),
                    new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Time), nameof(Time.fixedDeltaTime))),
                    new CodeMatch(OpCodes.Ldc_R4, 3f),
                    new CodeMatch(OpCodes.Div),
                    new CodeMatch(OpCodes.Add),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(Wings), nameof(Wings.charge)))
                   )
                   .ThrowIfInvalid("Could not find CodeMatch target.")
                   .Advance(1)
                   .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(UpdraftLogic)))
                   );


        return codeMatcher.InstructionEnumeration();
    }
}