using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace WiCCompanionExpandedTweaks
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "WiCCompanionExpandedTweaks";
        private bool _patched;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            if (_patched) return;
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            _patched = true;
        }
    }
}