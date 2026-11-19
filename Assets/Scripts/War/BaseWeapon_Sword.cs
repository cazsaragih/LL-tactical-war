namespace LLTacticalWar
{
    // New: this class had no weapon wired up in 2015.
    public class BaseWeapon_Sword : BaseWeapon
    {
        void Awake() { weaponLength = 2.1f; swingDuration = 0.5f; }
    }
}
