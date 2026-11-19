namespace LLTacticalWar
{
    // New: this class had no weapon wired up in 2015.
    public class BaseWeapon_Club : BaseWeapon
    {
        void Awake() { weaponLength = 2.2f; swingDuration = 0.7f; }
    }
}
