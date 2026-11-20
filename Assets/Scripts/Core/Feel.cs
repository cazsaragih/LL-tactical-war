namespace LLTacticalWar
{
    // What a press on the board gets back: a short sound and, on the same frame, the map pin's answer
    // (a dip as it moves, a stab and an ink ripple on confirm, a lift on cancel, a shake on a refusal).
    // Menus answer through MenuView (the ink underline, the rows' slide).
    public enum Ack { Move, Confirm, Cancel, Refuse }

    public static class Feel
    {
        public static void Ack(Ack a, float volume = 1f)
        {
            var bank = AudioBank.I;
            switch (a)
            {
                case LLTacticalWar.Ack.Move: bank?.Play(Sfx.UiMove, 0.35f * volume); break;
                case LLTacticalWar.Ack.Confirm: bank?.Play(Sfx.UiConfirm, volume); break;
                case LLTacticalWar.Ack.Cancel: bank?.Play(Sfx.UiCancel, volume); break;
                case LLTacticalWar.Ack.Refuse: bank?.Play(Sfx.UiCancel, 0.6f * volume); break;
            }
            if (PointerScript.Active != null) PointerScript.Active.Answer(a);
        }
    }
}
