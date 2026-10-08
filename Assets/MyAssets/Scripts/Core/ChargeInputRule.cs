namespace HandHero.Core
{
    public enum ChargeInputAction
    {
        None,   // not charging (a charge in progress is released by ChargeShotModel)
        Hold,   // keep charging
        Cancel, // drop the charge without firing
    }

    // Decides what this frame means for the charge shot. A held pinch charges;
    // losing the aim hand, a switched-off input source (pause, round end: default
    // data has HasAim = false) or a dead hero cancel it, so no charge shot fires
    // into a paused frame. The legacy palms charge keeps firing at the last aim
    // point when the aim hand drops out while the hands overlap.
    public static class ChargeInputRule
    {
        public static ChargeInputAction Decide(HandInputData input, bool heroAlive)
        {
            if (!heroAlive) return ChargeInputAction.Cancel;
            if (input.Has(HandGestures.ChargeHeld)) return ChargeInputAction.Hold;
            if (!input.HasAim) return ChargeInputAction.Cancel;
            return input.PinchHeld ? ChargeInputAction.Hold : ChargeInputAction.None;
        }
    }
}
