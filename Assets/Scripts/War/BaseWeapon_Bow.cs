namespace LLTacticalWar
{
    // New: this class had no weapon wired up in 2015.
    public class BaseWeapon_Bow : BaseWeapon
    {
        void Awake() { weaponLength = 11f; swingDuration = 1.1f; hitStart = 0.5f; hitEnd = 0.6f; ranged = true; }
    }
}
