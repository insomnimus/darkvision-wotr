using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Dungeon;
using Kingmaker.Dungeon.Enums;
using Kingmaker.UI.MVVM._PCView.MapIslands;
using Kingmaker.UI.MVVM._VM.MapIslands;
using UnityEngine;
using WrathAccess.UI;
using WrathAccess.UI.Graph;

namespace WrathAccess.Screens
{
    public sealed class MapIslandsScreen : Screen
    {
        private static readonly System.Reflection.FieldInfo IslandState =
            AccessTools.Field(typeof(MapIslandItemVM), "m_IslandState");
        private static readonly System.Reflection.MethodInfo SelectIsland =
            AccessTools.Method(typeof(MapIslandItemView), "OnSelect");

        public override string Key => "ctx.islands";
        public override string ScreenName => Loc.T("islands.title");
        public override int Layer => 15;
        public override bool Exclusive => true;

        private static MapIslandsVM Vm() =>
            Game.Instance?.RootUiContext?.InGameVM?.StaticPartVM?.MapIslandsVM?.Value;

        public override bool IsActive() => Vm() != null;

        public override void Build(GraphBuilder b)
        {
            var vm = Vm();
            if (vm == null) return;

            if (vm.IsTraveling.Value)
            {
                b.BeginStop("travel").AddItem(ControlId.Referenced(vm, "travel"),
                    GraphNodes.Text(() => Loc.T("islands.traveling")));
                return;
            }

            var islands = vm.IslandsVm.Append(vm.FinalIsland)
                .Where(item => item != null && item.IsVisible)
                .Select(item => (Item: item, State: (DungeonIslandState)IslandState.GetValue(item)))
                .Where(entry => entry.State != null)
                .OrderBy(entry => entry.State.GetNumber())
                .ThenBy(entry => entry.State.Position.x)
                .ThenBy(entry => entry.State.Position.y);
            var destinations = new List<(MapIslandItemVM Item, string Name)>();
            var route = new List<(MapIslandItemVM Item, string Name)>();
            foreach (var stage in islands.GroupBy(entry => entry.State.GetNumber()))
            {
                int branch = 0;
                foreach (var entry in stage)
                {
                    branch++;
                    var name = entry.Item == vm.FinalIsland ? Loc.T("islands.final")
                        : stage.Count() > 1 ? Loc.T("islands.branch", new { number = stage.Key, branch })
                        : Loc.T("islands.number", new { number = stage.Key });
                    (entry.Item.IsAvailable.Value ? destinations : route).Add((entry.Item, name));
                }
            }

            EmitIslands(b, vm, destinations, "destinations", "islands.destinations");
            EmitIslands(b, vm, route, "route", "islands.route");
            b.BeginStop("close").AddItem(ControlId.Referenced(vm, "close"),
                GraphNodes.Button(() => Loc.T("action.close"), Close));
        }

        private static void EmitIslands(GraphBuilder b, MapIslandsVM vm,
            List<(MapIslandItemVM Item, string Name)> islands, string stop, string label)
        {
            if (islands.Count == 0) return;
            b.BeginStop(stop).PushContext(Loc.T(label), "list");
            foreach (var (item, name) in islands)
            {
                var node = GraphNodes.Button(() => Label(item, name),
                    () => Activate(vm, item), () => item.IsAvailable.Value);
                var tip = item.Tooltip ?? item.RewardVM?.Tooltip;
                if (tip != null) node.OnTooltip = () => TooltipScreen.Open(tip);
                b.AddItem(ControlId.ForObject(item), node);
            }
            b.PopContext();
        }

        private static string Label(MapIslandItemVM item, string name)
        {
            var state = item.IsCurrent.Value ? "islands.current"
                : item.IsSkipped.Value ? "islands.skipped"
                : item.IsVisited.Value ? "islands.visited"
                : item.IsAvailable.Value ? "islands.available"
                : "islands.unavailable";
            var parts = new List<string> { name, Loc.T(state) };
            switch (item.DifficultyVM?.Difficulty)
            {
                case DungeonDifficulty.Easy: parts.Add(Loc.T("islands.easy")); break;
                case DungeonDifficulty.Normal: parts.Add(Loc.T("islands.normal")); break;
                case DungeonDifficulty.Hard: parts.Add(Loc.T("islands.hard")); break;
            }
            var reward = TextUtil.StripRichText(item.RewardVM?.Name);
            if (!string.IsNullOrWhiteSpace(reward))
                parts.Add(Loc.T("islands.reward", new { name = reward }));
            return string.Join(", ", parts);
        }

        private static void Activate(MapIslandsVM vm, MapIslandItemVM item)
        {
            if (!ReferenceEquals(vm, Vm()) || vm.IsTraveling.Value || !item.IsVisible || !item.IsAvailable.Value)
                return;

            // The mouse marker selects the party or opens a confirmation before starting travel.
            var view = Object.FindObjectsOfType<MapIslandItemView>()
                .FirstOrDefault(v => v.isActiveAndEnabled && ReferenceEquals(v.GetViewModel(), item));
            if (view != null) SelectIsland.Invoke(view, null);
        }

        private static void Close() => Vm()?.Close();

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Message.Localized("ui", "action.close"), _ => Close());
        }
    }
}
