using System;
using System.Collections.Generic;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace WarcraftCS2.Gameplay;

    public static class WowRegistry
    {
        public static readonly Dictionary<string, IActiveSpell> Spells  = new();
        public static readonly Dictionary<string, ITalent>      Talents = new();
        public static readonly Dictionary<string, IWowClass>    Classes = new();

        public static void RegisterAllFromAssembly(Assembly asm)
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.IsAbstract || t.IsInterface) continue;

                if (typeof(IActiveSpell).IsAssignableFrom(t))
                {
                    if (Activator.CreateInstance(t) is IActiveSpell sp)
                        Spells[sp.Id] = sp;
                }
                else if (typeof(ITalent).IsAssignableFrom(t))
                {
                    if (Activator.CreateInstance(t) is ITalent tl)
                        Talents[tl.Id] = tl;
                }
                else if (typeof(IWowClass).IsAssignableFrom(t))
                {
                    if (Activator.CreateInstance(t) is IWowClass cls)
                        Classes[cls.Id] = cls;
                }
            }
        }

        public static void DispatchSpawn(IWowRuntime rt, CCSPlayerController player)
        {
            var prof = rt.GetProfile(player);
            if (!Classes.TryGetValue(prof.ClassId, out var cls)) return;

            foreach (var id in cls.InnateTalents)
                if (Talents.TryGetValue(id, out var t) && prof.Level >= t.MinLevel)
                    t.ApplyOnSpawn(rt, player, prof);

            foreach (var id in prof.Talents)
                if (Talents.TryGetValue(id, out var t) && prof.Level >= t.MinLevel)
                    t.ApplyOnSpawn(rt, player, prof);
        }

        public static void DispatchHurt(IWowRuntime rt, CCSPlayerController attacker, CCSPlayerController victim, EventPlayerHurt ev)
        {
            // таланты АТАКУЮЩЕГО (оффенсив)
            if (attacker is { IsValid: true })
            {
                var aprof = rt.GetProfile(attacker);
                if (Classes.TryGetValue(aprof.ClassId, out var aCls))
                {
                    foreach (var id in aCls.InnateTalents)
                        if (Talents.TryGetValue(id, out var t) && aprof.Level >= t.MinLevel)
                            t.OnPlayerHurt(rt, ev, victim, attacker, aprof);

                    foreach (var id in aprof.Talents)
                        if (Talents.TryGetValue(id, out var t) && aprof.Level >= t.MinLevel)
                            t.OnPlayerHurt(rt, ev, victim, attacker, aprof);
                }
            }

            // таланты ЖЕРТВЫ (дефенсив)
            if (victim is { IsValid: true })
            {
                var vprof = rt.GetProfile(victim);
                if (Classes.TryGetValue(vprof.ClassId, out var vCls))
                {
                    foreach (var id in vCls.InnateTalents)
                        if (Talents.TryGetValue(id, out var t) && vprof.Level >= t.MinLevel)
                            t.OnPlayerHurt(rt, ev, victim, attacker, vprof);

                    foreach (var id in vprof.Talents)
                        if (Talents.TryGetValue(id, out var t) && vprof.Level >= t.MinLevel)
                            t.OnPlayerHurt(rt, ev, victim, attacker, vprof);
                }
            }
        }
    }