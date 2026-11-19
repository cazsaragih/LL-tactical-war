namespace LLTacticalWar
{
    // The six 2015 classes. Names, IDs, descriptions, troop counts, attack ranges and terrain
    // movement are the original values. In 2015 every class had identical HP/ATK/DEF
    // (100/20/0); small differences now follow each class's own description.

    public class BaseClass_Apprentice : BaseClass
    {
        public BaseClass_Apprentice()
        {
            ClassID = 1; ClassName = "Apprentice"; ClassType = "Mage";
            ClassDescription = "Ranged character with powerful magic and low defense";
            Weapon = WeaponKind.Staff;
            BaseHP = 90; BaseAttack = 25; BaseDefense = 0; TotalUnits = 100;
            TileAttack = 2; WarSpeed = 0.95f;
            plainMove = 2; forestMove = 1; desertMove = 3; mountainMove = 3; fortMove = 1;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Apprentice();
    }

    public class BaseClass_Archer : BaseClass
    {
        public BaseClass_Archer()
        {
            ClassID = 2; ClassName = "Archer"; ClassType = "Archer";
            ClassDescription = "Ranged character with high agility and low defense";
            Weapon = WeaponKind.Bow;
            BaseHP = 90; BaseAttack = 18; BaseDefense = 2; TotalUnits = 50;
            TileAttack = 2; WarSpeed = 1.15f;
            plainMove = 2; forestMove = 1; desertMove = 1; mountainMove = 3; fortMove = 3;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Archer();
    }

    public class BaseClass_Brawler : BaseClass
    {
        public BaseClass_Brawler()
        {
            ClassID = 3; ClassName = "Brawler"; ClassType = "Beastman";
            ClassDescription = "Melee character with heavy weapons and high defense";
            Weapon = WeaponKind.Club;
            BaseHP = 120; BaseAttack = 22; BaseDefense = 8; TotalUnits = 50;
            TileAttack = 1; WarSpeed = 0.9f;
            plainMove = 2; forestMove = 3; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Brawler();
    }

    public class BaseClass_Dragoon : BaseClass
    {
        public BaseClass_Dragoon()
        {
            ClassID = 4; ClassName = "Dragoon"; ClassType = "Cavalier";
            ClassDescription = "Melee character with high attack and high defense";
            Weapon = WeaponKind.Spear; Mounted = true;
            BaseHP = 110; BaseAttack = 26; BaseDefense = 6; TotalUnits = 50;
            TileAttack = 1; WarSpeed = 1.35f;
            plainMove = 4; forestMove = 1; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Dragoon();
    }

    public class BaseClass_Fighter : BaseClass
    {
        public BaseClass_Fighter()
        {
            ClassID = 5; ClassName = "Fighter"; ClassType = "Soldier";
            ClassDescription = "Melee character with balanced status";
            Weapon = WeaponKind.Sword;
            BaseHP = 100; BaseAttack = 20; BaseDefense = 4; TotalUnits = 100;
            TileAttack = 1; WarSpeed = 1f;
            plainMove = 2; forestMove = 3; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Fighter();
    }

    public class BaseClass_Thief : BaseClass
    {
        public BaseClass_Thief()
        {
            ClassID = 6; ClassName = "Thief"; ClassType = "Bandit";
            ClassDescription = "Melee character with high agility and low defense";
            Weapon = WeaponKind.Dagger;
            BaseHP = 85; BaseAttack = 22; BaseDefense = 0; TotalUnits = 50;
            TileAttack = 1; WarSpeed = 1.25f;
            plainMove = 2; forestMove = 3; desertMove = 3; mountainMove = 1; fortMove = 1;
        }
        public override void BoostStats() { BaseHP += 150; BaseAttack += 10; }
        public override BaseClass Fresh() => new BaseClass_Thief();
    }
}
