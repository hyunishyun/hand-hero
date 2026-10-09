namespace HandHero.Core
{
    // All sound recipes in one place (D14): short, clean sci-fi synth. The beam is
    // the loudest; square and saw waves sit behind a low-pass so nothing is harsh.
    // A real recording assigned to the event's slot on SfxPlayer replaces the recipe.
    public static class SfxRecipes
    {
        public static SfxRecipe Get(SfxId id)
        {
            switch (id)
            {
                // Combat
                case SfxId.BeamFire:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 1400f, EndHz = 300f, Duration = 0.14f,
                        Decay = 0.05f, Sustain = 0.4f, Release = 0.06f, NoiseMix = 0.1f, LowPassHz = 4000f, Volume = 0.8f };
                case SfxId.EnemyFire:
                    return new SfxRecipe { Wave = SfxWave.Saw, StartHz = 700f, EndHz = 160f, Duration = 0.16f,
                        Decay = 0.06f, Sustain = 0.4f, Release = 0.07f, LowPassHz = 2500f, Volume = 0.55f };
                case SfxId.ChargeStart:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 220f, EndHz = 440f, Duration = 0.25f,
                        Attack = 0.03f, Release = 0.08f, VibratoHz = 8f, VibratoDepth = 0.02f, Volume = 0.4f };
                case SfxId.ChargeReady:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 880f, EndHz = 880f, Duration = 0.16f,
                        Release = 0.04f, Notes = new[] { 0f, 7f }, Volume = 0.45f };
                case SfxId.ChargeRelease:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 900f, EndHz = 120f, Duration = 0.3f,
                        Decay = 0.12f, Sustain = 0.35f, Release = 0.1f, NoiseMix = 0.25f, LowPassHz = 3000f, Volume = 0.8f };
                case SfxId.BotTelegraph:
                    // Two-tone warning: the dodge cue, clearly different from any shot.
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 520f, EndHz = 520f, Duration = 0.24f,
                        Attack = 0.01f, Release = 0.03f, Notes = new[] { 0f, 5f }, LowPassHz = 2000f, Volume = 0.6f };
                case SfxId.HitDealt:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 1200f, EndHz = 800f, Duration = 0.07f,
                        Decay = 0.03f, Sustain = 0.3f, Release = 0.03f, Volume = 0.55f };
                case SfxId.HitTaken:
                    return new SfxRecipe { Wave = SfxWave.Saw, StartHz = 180f, EndHz = 60f, Duration = 0.22f,
                        Decay = 0.08f, Sustain = 0.3f, Release = 0.08f, NoiseMix = 0.35f, LowPassHz = 1500f, Volume = 0.75f };
                case SfxId.Shockwave:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 160f, EndHz = 40f, Duration = 0.45f,
                        Attack = 0.01f, Decay = 0.2f, Sustain = 0.3f, Release = 0.15f, NoiseMix = 0.3f, LowPassHz = 900f,
                        Volume = 0.75f };
                case SfxId.BotDown:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 500f, EndHz = 60f, Duration = 0.4f,
                        Decay = 0.15f, Sustain = 0.3f, Release = 0.15f, NoiseMix = 0.4f, LowPassHz = 1800f, Volume = 0.7f };

                // Run flow
                case SfxId.CountdownTick:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 880f, EndHz = 880f, Duration = 0.08f,
                        Release = 0.05f, Volume = 0.5f };
                case SfxId.Fight:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 440f, EndHz = 440f, Duration = 0.36f,
                        Release = 0.04f, Notes = new[] { 0f, 7f, 12f }, LowPassHz = 3500f, Volume = 0.65f };
                case SfxId.IslandCleared:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 523f, EndHz = 523f, Duration = 0.48f,
                        Release = 0.05f, Notes = new[] { 0f, 4f, 7f, 12f }, Volume = 0.6f };
                case SfxId.ChestOpen:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 300f, EndHz = 900f, Duration = 0.3f,
                        Attack = 0.02f, Release = 0.1f, NoiseMix = 0.1f, VibratoHz = 12f, VibratoDepth = 0.03f, Volume = 0.5f };
                case SfxId.ItemPick:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 660f, EndHz = 660f, Duration = 0.18f,
                        Release = 0.04f, Notes = new[] { 0f, 7f }, Volume = 0.55f };
                case SfxId.ShopBuy:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 988f, EndHz = 988f, Duration = 0.18f,
                        Release = 0.03f, Notes = new[] { 0f, 4f, 7f }, LowPassHz = 4000f, Volume = 0.5f };
                case SfxId.Reroll:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 400f, EndHz = 1000f, Duration = 0.2f,
                        Release = 0.05f, VibratoHz = 20f, VibratoDepth = 0.05f, Volume = 0.45f };
                case SfxId.Denied:
                    return new SfxRecipe { Wave = SfxWave.Square, StartHz = 220f, EndHz = 220f, Duration = 0.22f,
                        Release = 0.03f, Notes = new[] { 0f, -1f }, LowPassHz = 1500f, Volume = 0.5f };
                case SfxId.PortalPick:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 200f, EndHz = 1200f, Duration = 0.4f,
                        Attack = 0.03f, Release = 0.12f, NoiseMix = 0.1f, VibratoHz = 6f, VibratoDepth = 0.03f, Volume = 0.55f };
                case SfxId.Victory:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 523f, EndHz = 523f, Duration = 0.9f,
                        Release = 0.05f, Notes = new[] { 0f, 4f, 7f, 12f, 7f, 12f }, Volume = 0.7f };
                case SfxId.Defeat:
                    return new SfxRecipe { Wave = SfxWave.Saw, StartHz = 330f, EndHz = 330f, Duration = 0.75f,
                        Release = 0.08f, Notes = new[] { 0f, -3f, -7f }, LowPassHz = 1500f, Volume = 0.6f };

                // Menus
                case SfxId.MenuPoint:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 1500f, EndHz = 1500f, Duration = 0.03f,
                        Release = 0.02f, Volume = 0.25f };
                case SfxId.MenuPress:
                    return new SfxRecipe { Wave = SfxWave.Triangle, StartHz = 900f, EndHz = 1300f, Duration = 0.06f,
                        Release = 0.03f, Volume = 0.4f };
                case SfxId.Pause:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 700f, EndHz = 350f, Duration = 0.15f,
                        Release = 0.05f, Volume = 0.4f };
                case SfxId.Resume:
                    return new SfxRecipe { Wave = SfxWave.Sine, StartHz = 350f, EndHz = 700f, Duration = 0.15f,
                        Release = 0.05f, Volume = 0.4f };
                default:
                    return default;
            }
        }
    }
}
