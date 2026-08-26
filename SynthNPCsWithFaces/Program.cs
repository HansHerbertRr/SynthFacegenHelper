using System;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace SynthNPCsWithFaces
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance.AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch).Run(args, new RunPreferences()
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher()
                    {
                        IdentifyingModKey = "SynthFacegenHelper.esp",
                        TargetRelease = GameRelease.SkyrimSE,
                    }
                });
        }
        
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {

            var modSkyrim = ModKey.FromFileName("Skyrim.esm");
            
            // Only races capable of having a FaceGen head are considered.
            var races = state.LoadOrder.PriorityOrder.Race()
                .WinningOverrides()
                .Where(race => race.Flags.HasFlag(Race.Flag.FaceGenHead))
                .Where(race => !race.Flags.HasFlag(Race.Flag.Child)) 
                .Where(race => !race.Name == InvisibleRace))
                .Where(race => !race.Flags.HasFlag(Race.Flag.Default))
                .Where(race => !race.Flags.HasFlag(Race.Flag.Manakin))
                .ToDictionary(race => race.FormKey);

            Console.WriteLine($"Found {races.Count} races");

            var vanillaNPCs = state.LoadOrder.PriorityOrder
                .TakeLast(Extensions.StockESMs.Count)
                .Npc()
                .WinningOverrides()
                .ToDictionary(r => r.FormKey);

            
            var burnedAstrid = new FormKey(modSkyrim, 0x04D6D0);
            var player = new FormKey(modSkyrim, 0x000007);

            /*
             * An NPC is included only when all of the following are true:
             *
             * 1. Its race has a FaceGen head.
             * 2. Its race is not a child race.
             * 3. It does NOT have a template with Use Traits enabled.
             * 4. It is NOT marked Is CharGen Face Preset.
             * 5. It is NOT Burned Astrid (AstridEnd)
             * 6. It is NOT the Player
             * 7. If it is a stock NPC, its winning record differs from the vanilla record.
             *
             * Mod-added NPCs are included as long as they satisfy the other criteria.
             */
            var npcs = state.LoadOrder.PriorityOrder.Npc()
                .WinningOverrides()
                .Where(npc => races.ContainsKey(npc.Race.FormKey))
                .Where(npc => npc.Template.IsNull || !npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
                .Where(npc => !npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.IsCharGenFacePreset))
                .Where(npc => npc.FormKey != burnedAstrid)
                .Where(npc => npc.FormKey != player)
                .Where(npc =>
                {
                    if (vanillaNPCs.TryGetValue(npc.FormKey, out var vanillaNPC))
                    {
                        return !npc.Equals(vanillaNPC);
                    }
                    return true;
                })
                .Select(npc => npc.DeepCopy())
                .ToArray();


            Console.WriteLine($"Found {npcs.Length} NPCs");
            state.PatchMod.Npcs.Set(npcs);


            // HEAD PARTS
            var vanillaHeadParts = state.LoadOrder.PriorityOrder
                .TakeLast(Extensions.StockESMs.Count)
                .HeadPart()
                .WinningOverrides()
                .ToDictionary(r => r.FormKey);

            var headParts = state.LoadOrder.PriorityOrder
                .HeadPart()
                .WinningOverrides()
                .NoStockRecords()
                .Where(headPart =>
                {
                    if (vanillaHeadParts.TryGetValue(headPart.FormKey, out var vanillaHeadPart))
                    {
                        return !vanillaHeadPart.Equals(headPart);
                    }
                    return true;
                })
                .Select(headPart => headPart.DeepCopy())
                .ToArray();


            Console.WriteLine($"Found {headParts.Length} Head Parts");
            state.PatchMod.HeadParts.Set(headParts);


            // COLORS
            var vanillaColors = state.LoadOrder.PriorityOrder
                .TakeLast(Extensions.StockESMs.Count)
                .ColorRecord()
                .WinningOverrides()
                .ToDictionary(r => r.FormKey);

            var colors = state.LoadOrder.PriorityOrder
                .ColorRecord()
                .WinningOverrides()
                .NoStockRecords()
                .Where(color =>
                {
                    if (vanillaColors.TryGetValue(color.FormKey, out var vanillaColor))
                    {
                        return !vanillaColor.Equals(color);
                    }
                    return true;
                })
                .Select(color => color.DeepCopy())
                .ToArray();

            Console.WriteLine($"Found {colors.Length} Colors");
            state.PatchMod.Colors.Set(colors);
        }
    }
}
