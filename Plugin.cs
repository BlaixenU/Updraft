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
        Logger.LogInfo($"NOTE: Updraft will not work without Masquerade Divinity installed.");
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
public class Updraft
{
    private static int remainingDrafts;

    private const int maxDrafts = 1;

    private static bool pressedJump;
    
    private static GameObject InstantiateForAudio()
    {
        GameObject soundObject = new GameObject("UpdraftSound", typeof(AudioSource));
        
        // theres alot that remains to be done here hahahahahahahahd klfjsdal;fkdjsal;
        // figure out a delayed Destroy() call (couldve sworn there was an ultrakill class for this)
        // initialize all relevant fields in soundObject's Audio Source component
        // figure out how to reference a certain masquerade divinity asset to use for the sound

        
        soundObject.transform.SetParent(NewMovement.Instance.transform, false);

        return soundObject;
    }

    private static void UpdraftLogic(Wings wings)
    {
        NewMovement player = wings.mov;
        float sinceGrounded = player.gc.sinceLastGrounded;

        if (player.gc.onGround)
        {
            remainingDrafts = maxDrafts;
            return;
        }
        else if (sinceGrounded > 0.1)
        {
            if (remainingDrafts > 0 && pressedJump)
            {
                remainingDrafts -= 1;

                Vector3 velocityBuffer = player.rb.velocity; // ignore that i probably dont know what buffer means
                velocityBuffer.y = Mathf.Max(velocityBuffer.y, 25f);
                player.rb.velocity = velocityBuffer;

                Plugin.Logger.LogInfo("Updrafted");
            }
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(NewMovement), nameof(NewMovement.Parry))]
    private static void ResetDraftsOnParry()
    {
        remainingDrafts = maxDrafts;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Wings), nameof(Wings.Update))]
    private static void InputCheck(Wings __instance)
    {
        pressedJump = __instance.input.Jump.WasPerformedThisFrame;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Wings), nameof(Wings.FixedUpdate))]
    private static IEnumerable<CodeInstruction> LogicInsert(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        // ADD UPDRAFT LOGIC

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
                   .ThrowIfInvalid("Could not find CodeMatch target 1.")
                   .Advance(1)
                   .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Updraft), nameof(UpdraftLogic)))
                   );

        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Wings), nameof(Wings.FixedUpdate))]
    private static IEnumerable<CodeInstruction> WingsChanges(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codeMatcher = new CodeMatcher(instructions, generator);

        // GIVE WINGS SLIGHT UPWARD MOMENTUM

        codeMatcher.Start()
                   .MatchForward(true,
                    /* new CodeMatch(OpCodes.Ldloca_S, 0),
                    new CodeMatch(OpCodes.Ldc_R4, 0.0), */
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(Vector3), "y"))
                   )
                   .ThrowIfInvalid("Could not find CodeMatch target 2.")
                   .MatchBack(true,
                    new CodeMatch(OpCodes.Ldc_R4, 0.0)
                   )
                   .ThrowIfInvalid("How did this not work")
                   .Set(OpCodes.Ldc_R4, 2.0f);
                


        return codeMatcher.InstructionEnumeration();
    }
}

