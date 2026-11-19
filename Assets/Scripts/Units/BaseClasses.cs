namespace LLTacticalWar
{
    // The six 2015 classes. Names, IDs, descriptions, weapons and terrain movement are the
    // original values. Stats are on the 1v1 scale and follow each class's own description.

    public class BaseClass_Apprentice : BaseClass
    {
        public BaseClass_Apprentice()
        {
            ClassID = 1; ClassName = "Apprentice"; ClassType = "Mage";
            ClassDescription = "Ranged character with powerful magic and low defense";
            Weapon = WeaponKind.Staff;
            Base = new Stats { HP = 20, Str = 0, Mag = 10, Skl = 7, Spd = 7, Lck = 5, Def = 3, Res = 8 };
            plainMove = 2; forestMove = 1; desertMove = 3; mountainMove = 3; fortMove = 1;
        }
    }

    public class BaseClass_Archer : BaseClass
    {
        public BaseClass_Archer()
        {
            ClassID = 2; ClassName = "Archer"; ClassType = "Archer";
            ClassDescription = "Ranged character with high agility and low defense";
            Weapon = WeaponKind.Bow;
            Base = new Stats { HP = 22, Str = 8, Mag = 0, Skl = 10, Spd = 9, Lck = 4, Def = 4, Res = 3 };
            plainMove = 2; forestMove = 1; desertMove = 1; mountainMove = 3; fortMove = 3;
        }
    }

    public class BaseClass_Brawler : BaseClass
    {
        public BaseClass_Brawler()
        {
            ClassID = 3; ClassName = "Brawler"; ClassType = "Beastman";
            ClassDescription = "Melee character with heavy weapons and high defense";
            Weapon = WeaponKind.Club;
            Base = new Stats { HP = 32, Str = 12, Mag = 0, Skl = 5, Spd = 5, Lck = 2, Def = 6, Res = 1 };
            plainMove = 2; forestMove = 3; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
    }

    public class BaseClass_Dragoon : BaseClass
    {
        public BaseClass_Dragoon()
        {
            ClassID = 4; ClassName = "Dragoon"; ClassType = "Cavalier";
            ClassDescription = "Melee character with high attack and high defense";
            Weapon = WeaponKind.Spear; Mounted = true;
            Base = new Stats { HP = 28, Str = 10, Mag = 0, Skl = 7, Spd = 7, Lck = 3, Def = 8, Res = 2 };
            plainMove = 4; forestMove = 1; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
    }

    public class BaseClass_Fighter : BaseClass
    {
        public BaseClass_Fighter()
        {
            ClassID = 5; ClassName = "Fighter"; ClassType = "Soldier";
            ClassDescription = "Melee character with balanced status";
            Weapon = WeaponKind.Sword;
            Base = new Stats { HP = 26, Str = 9, Mag = 0, Skl = 9, Spd = 8, Lck = 5, Def = 7, Res = 2 };
            plainMove = 2; forestMove = 3; desertMove = 1; mountainMove = 1; fortMove = 3;
        }
    }

    public class BaseClass_Thief : BaseClass
    {
        public BaseClass_Thief()
        {
            ClassID = 6; ClassName = "Thief"; ClassType = "Bandit";
            ClassDescription = "Melee character with high agility and low defense";
            Weapon = WeaponKind.Dagger;
            Base = new Stats { HP = 21, Str = 7, Mag = 0, Skl = 11, Spd = 12, Lck = 7, Def = 3, Res = 3 };
            plainMove = 2; forestMove = 3; desertMove = 3; mountainMove = 1; fortMove = 1;
        }
    }
}
