using HarmonyLib;
using KSP.Game;
using KSP.Messages;
using MonoMod.Cil;
using SpaceWarp.Patching.LoadingActions;
using SpaceWarp.API.Game;

namespace SpaceWarp.Patching;

[HarmonyPatch]
internal static class BootstrapPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Awake))]
    private static void GetSpaceWarpMods()
    {
        SpaceWarpManager.GetSpaceWarpPlugins();
    }
    
    [HarmonyILManipulator]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartBootstrap))]
    private static void PatchInitializationsIL(ILContext il, ILLabel endLabel)
    {
        ILCursor c = new(il);

        var flowProp = AccessTools.DeclaredProperty(typeof(GameManager), nameof(GameManager.LoadingFlow));

        c.GotoNext(MoveType.After,
            x => x.MatchCallOrCallvirt(flowProp.SetMethod)
        );

        c.EmitDelegate(static () =>
        {
            foreach (var plugin in SpaceWarpManager.SpaceWarpPlugins)
            {
                GameManager.Instance.LoadingFlow.AddAction(new PreInitializeModAction(plugin));
            }
        });
        
        c.GotoLabel(endLabel, MoveType.Before);
        c.Index -= 1;
        c.EmitDelegate(static () =>
        {
            var flow = GameManager.Instance.LoadingFlow;
            
            GameManager.Instance.Game.Messages.Subscribe(typeof(GameStateEnteredMessage), StateChanges.OnGameStateEntered,false,true);
            GameManager.Instance.Game.Messages.Subscribe(typeof(GameStateLeftMessage), StateChanges.OnGameStateLeft,false,true);
            flow.AddAction(new LoadSpaceWarpLocalizationsAction());
            flow.AddAction(new LoadSpaceWarpAddressablesAction());
            flow.AddAction(new SpaceWarpAssetInitializationAction());
            flow.AddAction(new InitializeSpaceWarpUIAction());

            foreach (var plugin in SpaceWarpManager.SpaceWarpPlugins)
            {
                flow.AddAction(new LoadLocalizationAction(plugin));
                flow.AddAction(new LoadAddressablesAction(plugin));
                flow.AddAction(new LoadAssetAction(plugin));
            }
            
            foreach (var plugin in SpaceWarpManager.SpaceWarpPlugins)
            {
                flow.AddAction(new InitializeModAction(plugin));
            }
            
            foreach (var plugin in SpaceWarpManager.SpaceWarpPlugins)
            {
                flow.AddAction(new PostInitializeModAction(plugin));
            }
        });
    }
}
