using System.Linq;
using HarmonyLib;
using MGAutoSell;
using RealisticOrbitalTrade;
using RealisticOrbitalTrade.Patch;
using RimWorld;
using Verse;

namespace RealisticOrbitalTradeAddons.FindTrade
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            new Harmony("ifchen0.realisticorbitaltrade.addons.findtrade").PatchAll();
        }
    }

    /// <summary>
    /// Find / Trade's comms-console barter job trades with every passing ship through
    /// TradeSession.SetupWith + TradeDeal.TryExecute, never calling TradeShip.TryOpenComms,
    /// so Realistic Orbital Trade never starts a trade agreement and the deal falls back to
    /// vanilla instant delivery. This wraps that loop so each ship gets an agreement.
    /// </summary>
    [HarmonyPatch(typeof(TradeDealProcessor), nameof(TradeDealProcessor.DoTradeShips))]
    public static class Patch_DoTradeShips
    {
        internal static bool Active;

        public static bool Prefix(Pawn socialPawn)
        {
            // A renegotiation dialog owns the static agreement slot; leave it alone.
            if (Utils.RenegotiatingAgreement != null || TradeShipData.tradeAgreementForQuest != null)
                return false;

            // Ships ROT would not answer (active trade, comms closing, blacklist, amends) are
            // marked as handled so Find / Trade skips them instead of trading the vanilla way.
            var traded = Current.Game.GetComponent<TradeRulesGameComp>().traders;
            var standing = RealisticOrbitalTradeGameComponent.Current.Standing;
            foreach (var ship in socialPawn.Map.passingShipManager.passingShips.OfType<TradeShip>())
            {
                if (traded.Contains(ship))
                    continue;
                var data = ship.GetData();
                var outcome = Rimworld_TradeShip_TryOpenComms.DecideComms(
                    data.ticksUntilCommsClosed, Utils.IsMakingAmends, standing, data.HasActiveTradeAgreement);
                if (outcome != Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement)
                    traded.Add(ship);
            }

            Active = true;
            return true;
        }

        public static void Finalizer()
        {
            if (!Active)
                return;
            Active = false;
            // Left over when the last ship had nothing to trade or ROT rejected the deal.
            TradeShipData.EndTradeAgreementIfExists();
        }
    }

    /// <summary>
    /// Starts the ROT trade agreement for each ship Find / Trade sets up, mirroring
    /// ROT's own TryOpenComms prefix (it must exist before the deal adds its tradeables).
    /// </summary>
    [HarmonyPatch(typeof(TradeSession), nameof(TradeSession.SetupWith))]
    public static class Patch_SetupWith
    {
        public static void Prefix(ITrader __0, Pawn __1)
        {
            if (!Patch_DoTradeShips.Active || __0 is not TradeShip ship)
                return;
            // The previous ship's agreement is still here only if no trade went through.
            TradeShipData.EndTradeAgreementIfExists();
            ship.GetData().activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
                RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(ship, __1);
        }
    }
}
