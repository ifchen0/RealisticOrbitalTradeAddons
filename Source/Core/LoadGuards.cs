using System.Linq;
using HarmonyLib;
using RealisticOrbitalTrade;
using RealisticOrbitalTrade.Core;
using RimWorld;
using Verse;

namespace RealisticOrbitalTradeAddons
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            new Harmony("ifchen0.realisticorbitaltrade.addons").PatchAll();
        }
    }

    // Once a trade shuttle has flown off, its shipThing is destroyed but still referenced, so
    // Realistic Orbital Trade keeps working until the quest ends. Saving in that window (the
    // 500-tick delay after delivery, or between the sold goods leaving and the purchase
    // arriving) writes shipThing as null, and the patches below keep loading such a save from
    // throwing and aborting the load.

    /// <summary>
    /// Skips the shuttle cargo rebuild on load when a to-trader shuttle has already left. The
    /// sold things went with it, so they no longer resolve; the original would then hit the
    /// missing shuttle comp, and would wrongly report the delivered goods as lost.
    /// </summary>
    [HarmonyPatch(typeof(TradeAgreement), nameof(TradeAgreement.PruneDestroyedSoldItems))]
    public static class Patch_PruneDestroyedSoldItems
    {
        public static bool Prefix(TradeAgreement __instance)
        {
            if (!__instance.ToTraderTransportShips.Any(ship => ship.shipThing == null))
                return true;
            __instance.RemoveUnresolvedSoldEntries();
            return false;
        }
    }

    /// <summary>
    /// Returns an empty container instead of throwing when the to-player shuttle has already
    /// delivered and left; nothing bought is waiting on it any more.
    /// </summary>
    [HarmonyPatch(typeof(TradeDeliveryMethod_TransportShip), nameof(TradeDeliveryMethod_TransportShip.Things), MethodType.Getter)]
    public static class Patch_TransportShipThings
    {
        static readonly AccessTools.FieldRef<TradeDeliveryMethod_TransportShip, TransportShip> ship =
            AccessTools.FieldRefAccess<TradeDeliveryMethod_TransportShip, TransportShip>("<toPlayerTransportShip>P");

        public static bool Prefix(TradeDeliveryMethod_TransportShip __instance, ref ThingOwner __result)
        {
            if (ship(__instance)?.shipThing != null)
                return true;
            __result = new ThingOwner<Thing>();
            return false;
        }
    }
}
