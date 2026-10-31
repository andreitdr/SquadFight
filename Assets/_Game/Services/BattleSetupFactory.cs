using Game.Core;

namespace Game.Services
{
    /// <summary>Builds a battle from the current party (final stars + equipment + skill levels) and a fight definition.</summary>
    public static class BattleSetupFactory
    {
        public static BattleSetup Create(GameSession session, FightDef fight)
        {
            var db = session.Db;
            var setup = new BattleSetup { Rules = db.Combat };
            var slots = session.Party.GetSlots();
            for (var i = 0; i < slots.Length; i++)
            {
                var id = slots[i];
                if (id == null || !session.Roster.IsUnlocked(id)) continue;
                setup.Players.Add(BattleSetupBuilder.Hero(db.GetHero(id), session.Roster.GetStar(id), session.Inventory.GetEquipmentStats(id), i, db,
                    session.Roster.GetSkillLevel(id, HeroSkillSlot.Active), session.Roster.GetSkillLevel(id, HeroSkillSlot.Passive)));
            }
            BattleSetupBuilder.AddFightWaves(setup, fight, db);
            return setup;
        }
    }
}
