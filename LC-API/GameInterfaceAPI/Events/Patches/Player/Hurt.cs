﻿using GameNetcodeStuff;
using HarmonyLib;
using LC_API.GameInterfaceAPI.Events.EventArgs.Player;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace LC_API.GameInterfaceAPI.Events.Patches.Player
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
    internal class Hurt
    {
        private static HurtingEventArgs CallHurtingEvent(PlayerControllerB playerController, int damage, bool hasSFX, CauseOfDeath causeOfDeath,
            int deathAnimation, bool fallDamage, Vector3 force)
        {
            if (Plugin.configVanillaSupport.Value) return null;

            Features.Player player = Features.Player.GetOrAdd(playerController);

            HurtingEventArgs ev = new HurtingEventArgs(player, damage, hasSFX,
                causeOfDeath, deathAnimation, fallDamage, force);

            Handlers.Player.OnHurting(ev);

            player.CallHurtingOnOtherClients(damage, hasSFX, causeOfDeath, deathAnimation, fallDamage, force);

            return ev;
        }

        private static HurtEventArgs CallHurtEvent(PlayerControllerB playerController, int damage, bool hasSFX, CauseOfDeath causeOfDeath,
            int deathAnimation, bool fallDamage, Vector3 force)
        {
            Features.Player player = Features.Player.GetOrAdd(playerController);

            HurtEventArgs ev = new HurtEventArgs(player, damage, hasSFX,
                causeOfDeath, deathAnimation, fallDamage, force);

            Handlers.Player.OnHurt(ev);

            player.CallHurtOnOtherClients(damage, hasSFX, causeOfDeath, deathAnimation, fallDamage, force);

            return ev;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

            {
                const int offset = 3;

                int index = newInstructions.FindLastIndex(i => i.OperandIs(AccessTools.Method(typeof(PlayerControllerB),
                    nameof(PlayerControllerB.AllowPlayerDeath)))) + offset;

                Label nullLabel = generator.DefineLabel();
                Label notAllowedLabel = generator.DefineLabel();
                Label skipLabel = generator.DefineLabel();

                CodeInstruction[] inst = new CodeInstruction[]
                {
                    // HurtingEventArgs ev = Hurt.CallHurtingEvent(PlayerControllerB, int, bool, CauseOfDeath, int, bool, Vector3)
                    new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldarg, 4),
                    new CodeInstruction(OpCodes.Ldarg, 5),
                    new CodeInstruction(OpCodes.Ldarg, 6),
                    new CodeInstruction(OpCodes.Ldarg, 7),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hurt), nameof(Hurt.CallHurtingEvent))),
                    new CodeInstruction(OpCodes.Dup),

                    // if (ev is null) -> base game code
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brfalse_S, nullLabel),

                    // if (!ev.IsAllowed) return
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.IsAllowed))),
                    new CodeInstruction(OpCodes.Brfalse_S, notAllowedLabel),

                    // Duplicating the stack is more memory efficient than making a local
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),

                    // damage = ev.Damage
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.Damage))),
                    new CodeInstruction(OpCodes.Starg_S, 1),

                    // hasDamageSFX = ev.HasSFX
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.HasSFX))),
                    new CodeInstruction(OpCodes.Starg_S, 2),

                    // causeOfDeath = ev.CauseOfDeath
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.CauseOfDeath))),
                    new CodeInstruction(OpCodes.Starg_S, 4),

                    // deathAnimation = ev.DeathAnimation
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.DeathAnimation))),
                    new CodeInstruction(OpCodes.Starg_S, 5),

                    // fallDamage = ev.FallDamage
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.FallDamage))),
                    new CodeInstruction(OpCodes.Starg_S, 6),

                    // force = ev.Force
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.Force))),
                    new CodeInstruction(OpCodes.Starg_S, 7),
                    new CodeInstruction(OpCodes.Br, skipLabel),
                    new CodeInstruction(OpCodes.Pop).WithLabels(nullLabel),
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Br, skipLabel),
                    new CodeInstruction(OpCodes.Pop).WithLabels(notAllowedLabel),
                    new CodeInstruction(OpCodes.Ret)
                };

                newInstructions.InsertRange(index, inst);

                newInstructions[index + inst.Length].labels.Add(skipLabel);
            }

            {
                const int offset = 1;

                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Stfld
                    && i.OperandIs(AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.health)))) + offset;

                newInstructions.InsertRange(index, new CodeInstruction[]
                {
                    // HurtEventArgs ev = Hurt.CallHurtEvent(PlayerControllerB, int, bool, CauseOfDeath, int, bool, Vector3)
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldarg, 4),
                new CodeInstruction(OpCodes.Ldarg, 5),
                new CodeInstruction(OpCodes.Ldarg, 6),
                new CodeInstruction(OpCodes.Ldarg, 7),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hurt), nameof(Hurt.CallHurtEvent))),

                // Duplicating the stack is more memory efficient than making a local
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Dup),

                // hasDamageSFX = ev.HasSFX
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.HasSFX))),
                new CodeInstruction(OpCodes.Starg_S, 2),

                // causeOfDeath = ev.CauseOfDeath
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.CauseOfDeath))),
                new CodeInstruction(OpCodes.Starg_S, 4),

                // deathAnimation = ev.DeathAnimation
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.DeathAnimation))),
                new CodeInstruction(OpCodes.Starg_S, 5),

                // fallDamage = ev.FallDamage
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.FallDamage))),
                new CodeInstruction(OpCodes.Starg_S, 6),

                // force = ev.Force
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(HurtingEventArgs), nameof(HurtingEventArgs.Force))),
                new CodeInstruction(OpCodes.Starg_S, 7),
                });
            }

            for (int i = 0; i < newInstructions.Count; i++) yield return newInstructions[i];
        }
    }
}