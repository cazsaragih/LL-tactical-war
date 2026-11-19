namespace LLTacticalWar
{
    // 2015 length was 5 (shortened: the field is tighter now).
    public class BaseWeapon_Spear : BaseWeapon
    {
        void Awake() { weaponLength = 3.2f; swingDuration = 0.6f; }
    }
}
