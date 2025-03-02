﻿using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.BounceHelper
{
    public class BounceHelperModule : EverestModule
    {
        public static BounceHelperModule Instance { get; private set; }

        public override Type SettingsType => typeof(BounceHelperModuleSettings);
        public static BounceHelperModuleSettings Settings => (BounceHelperModuleSettings)Instance._Settings;

        public static SpriteBank spriteBank;

        public static Session session = null;

        private static bool isEnabled => (session != null && session.GetFlag("bounceModeEnabled")) || Settings.ForceBounceMode;
        private static bool useVanillaThrowBehaviour => (session != null && session.GetFlag("bounceModeUseVanillaThrowBehaviour"));
        private static bool useVanillaPickupBehaviour => (session != null && session.GetFlag("bounceModeUseVanillaPickupBehaviour"));

        #region Vanilla constants
        private const float DashSpeed = 240f;
        private const float DashAttackTime = 0.3f;
        private const float SuperJumpH = 260f;
        private const float JumpSpeed = -105f;
        private const float JumpHBoost = 40f;
        private const float WallJumpHSpeed = 90f + JumpHBoost;
        private const float DuckSuperJumpXMult = 1.25f;
        private const float DuckSuperJumpYMult = 0.5f;
        private const float VarJumpTime = 0.2f;
        private const float SuperWallJumpH = 90f + 40f * 2;
        private const float SuperWallJumpSpeed = -160f;
        private const float SuperWallJumpVarTime = 0.25f;
        private const int WallJumpCheckDist = 3;
        private const float WallJumpForceTime = 0.16f;
        private const float GliderWallJumpForceTime = 0.26f;
        private const float DashGliderBoostTime = 0.55f;
        private const float ThrowRecoil = 80f;
        private const float LiftXCap = 250f;
        private const float LiftYCap = 130f;
        private const float PickupTime = 0.16f;
        #endregion

        #region Setup
        public BounceHelperModule()
        {
            Instance = this;
#if DEBUG
            // debug builds use verbose logging
            Logger.SetLogLevel(nameof(BounceHelperModule), LogLevel.Verbose);
#else
            // release builds use info logging to reduce spam in log files
            Logger.SetLogLevel(nameof(BounceHelperModule), LogLevel.Info);
#endif
        }

        public override void Load()
        {

            #region Bouncing + neutral jump disabling
            On.Celeste.Player.WallJump += modWallJump;
            On.Celeste.Player.DashUpdate += modDashUpdate;
            On.Celeste.Player.DreamDashBegin += modDreamDashBegin;
            On.Celeste.Player.DreamDashEnd += modDreamDashEnd;
            On.Celeste.Player.SuperJump += modSuperJump;
            On.Celeste.Player.SuperWallJump += modSuperWallJump;
            On.Celeste.Player.Jump += modJump;
            On.Celeste.Player.NormalUpdate += modNormalUpdate;
            On.Celeste.Player.Update += modUpdate;
            #endregion

            #region Dash disabling
            On.Celeste.Player.StartDash += modStartDash;
            #endregion

            #region Speed tech stuff
            IL.Celeste.Player.CallDashEvents += modCallDashEvents;
            #endregion

            #region Moving block sliding player attachment
            On.Celeste.Player.IsRiding_Solid += modIsRiding;
            #endregion

            #region Jellyfish stuff
            On.Celeste.Player.Throw += modThrow;
            On.Celeste.Player.Die += modDie;
            On.Celeste.Player.RefillDash += modRefillDash;
            On.Celeste.Spring.OnCollide += modSpringOnCollide;
            On.Celeste.Player.PickupCoroutine += modPickupCoroutine;
            IL.Celeste.Player.NormalUpdate += modNormalUpdate;
            On.Monocle.Engine.Update += modEngineUpdate;
            On.Celeste.Level.EnforceBounds += modLevelEnforceBounds;
            #endregion

            #region Misc
            On.Celeste.Player.ClimbCheck += modClimbCheck;
            On.Celeste.Player.ClimbJump += modClimbJump;
            On.Celeste.Player.DashBegin += modDashBegin;
            On.Celeste.Level.Begin += onLevelBegin;
            Everest.Events.CustomBirdTutorial.OnParseCommand += CustomBirdTutorial_OnParseCommand;
            #endregion

            #region Replace vanilla entities with BounceHelper equivalents
            Everest.Events.Level.OnLoadEntity += ReplaceVanillaEntities;
            #endregion
        }

        public override void Unload()
        {

            #region Bouncing + neutral jump disabling
            On.Celeste.Player.WallJump -= modWallJump;
            On.Celeste.Player.DashUpdate -= modDashUpdate;
            On.Celeste.Player.DreamDashBegin -= modDreamDashBegin;
            On.Celeste.Player.DreamDashEnd -= modDreamDashEnd;
            On.Celeste.Player.SuperJump -= modSuperJump;
            On.Celeste.Player.SuperWallJump -= modSuperWallJump;
            On.Celeste.Player.Jump -= modJump;
            On.Celeste.Player.NormalUpdate -= modNormalUpdate;
            On.Celeste.Player.Update -= modUpdate;
            #endregion

            #region Dash disabling
            On.Celeste.Player.StartDash -= modStartDash;
            #endregion

            #region Speed tech stuff
            IL.Celeste.Player.CallDashEvents -= modCallDashEvents;
            #endregion

            #region Moving block sliding player attachment
            On.Celeste.Player.IsRiding_Solid -= modIsRiding;
            #endregion

            #region Jellyfish stuff
            On.Celeste.Player.Throw -= modThrow;
            On.Celeste.Player.Die -= modDie;
            On.Celeste.Player.RefillDash -= modRefillDash;
            On.Celeste.Spring.OnCollide -= modSpringOnCollide;
            On.Celeste.Player.PickupCoroutine -= modPickupCoroutine;
            IL.Celeste.Player.NormalUpdate -= modNormalUpdate;
            On.Monocle.Engine.Update -= modEngineUpdate;
            On.Celeste.Level.EnforceBounds -= modLevelEnforceBounds;
            #endregion

            #region Misc
            On.Celeste.Player.ClimbCheck -= modClimbCheck;
            On.Celeste.Player.ClimbJump -= modClimbJump;
            On.Celeste.Player.DashBegin -= modDashBegin;
            On.Celeste.Level.Begin -= onLevelBegin;
            Everest.Events.CustomBirdTutorial.OnParseCommand -= CustomBirdTutorial_OnParseCommand;
            #endregion

            #region Replace vanilla entities with BounceHelper equivalents
            Everest.Events.Level.OnLoadEntity -= ReplaceVanillaEntities;
            #endregion
        }

        public override void Initialize()
        {
            base.Initialize();

            #region Dream bounce particles
            ParticleType particle = new ParticleType(BounceBlock.P_FireBreak);
            particle.ColorMode = ParticleType.ColorModes.Choose;
            particle.Color = new Color(212, 79, 68); // Red.
            particle.Color2 = new Color(243, 0, 209); // Pink.
            dreamBounceParticles.Add(particle);

            particle = new ParticleType(particle);
            particle.Color = new Color(83, 206, 230); // Light blue.
            particle.Color2 = new Color(79, 105, 227); // Dark blue.
            dreamBounceParticles.Add(particle);

            particle = new ParticleType(particle);
            particle.Color = new Color(115, 277, 87); // Light green.
            particle.Color2 = new Color(0, 161, 4); // Dark green.
            dreamBounceParticles.Add(particle);

            particle = new ParticleType(particle);
            particle.Color = new Color(243, 242, 5); // Yellow.
            particle.Color2 = new Color(243, 242, 5); // Yellow.
            dreamBounceParticles.Add(particle);
            #endregion

            spriteBank = new SpriteBank(GFX.Game, "Graphics/BounceSprites.xml");
        }
        #endregion

        #region Bouncing + neutral jump disabling
        private static Vector2 sidewaysBounceSpeed = new Vector2(320, -55);
        private static Vector2 diagonalBounceSpeed = new Vector2(230, -170);
        private static Vector2 upwardsBounceSpeed = new Vector2(100, -200);
        private static Vector2 downwardsBounceSpeed = new Vector2(0, -210);

        private static string horizontalBounceSound = "event:/char/madeline/jump_superslide";
        private static string diagonalBounceSound = "event:/char/madeline/jump_super";
        private static string verticalBounceSound = "event:/char/madeline/jump_superwall";

        // For move block bouncing
        private static int parallelBounceStrength = 1;
        private static int diagonalBounceStrength = 2;
        private static int perpendicularBounceStrength = 4;

        private bool cornerBounced = false;

        // For when bouncing in a one tile hole
        private bool holeBounced = false;

        // For conserving horizontal speed when downward bouncing
        private static float conservedHSpeed = 0f;

        public static Color bounceColor = new Color(255, 191, 0);

        private bool dreamBounced = false;
        private const float dreamBounceSpeedMult = 1.2f;
        private static List<ParticleType> dreamBounceParticles = new List<ParticleType>();

        private Vector2 jellyfishBounceDir;
        private float jellyfishBounceTimer = 0f;
        private float jellyfishWallJumpForceTimer = 0f;

        private static MethodInfo playerWallJump = typeof(Player).GetMethod("WallJump", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo playerSuperJump = typeof(Player).GetMethod("SuperJump", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo playerSuperWallJump = typeof(Player).GetMethod("SuperWallJump", BindingFlags.Instance | BindingFlags.NonPublic);

        private void bounce(Player player, Vector2 bounceSpeed, int bounceStrength, Vector2 surfaceDir, bool dreamRipple = false, int wallCheckDistance = WallJumpCheckDist)
        {
            var playerData = getPlayerData(player);

            Vector2 liftSpeed = player.LiftSpeed;
            Solid bouncedSolid = player.CollideFirst<Solid>(player.Position + surfaceDir * wallCheckDistance);
            if (bouncedSolid != null && bouncedSolid.LiftSpeed != Vector2.Zero)
            {
                liftSpeed = bouncedSolid.LiftSpeed;
            }
            liftSpeed.X = Math.Min(Math.Abs(liftSpeed.X), LiftXCap) * Math.Sign(liftSpeed.X);
            liftSpeed.Y = Math.Min(Math.Abs(liftSpeed.Y), LiftYCap) * Math.Sign(liftSpeed.Y);
            bounceSpeed.X = bounceSpeed.X == 0 ? liftSpeed.X :
                Math.Max(Math.Abs(bounceSpeed.X + liftSpeed.X), Math.Abs(bounceSpeed.X)) * Math.Sign(bounceSpeed.X);
            bounceSpeed.Y = bounceSpeed.Y == 0 ? liftSpeed.Y :
                Math.Max(Math.Abs(bounceSpeed.Y + liftSpeed.Y), Math.Abs(bounceSpeed.Y)) * Math.Sign(bounceSpeed.Y);

            if (Math.Abs(conservedHSpeed) > 0)
            {
                if (Math.Abs(bounceSpeed.X) < Math.Abs(conservedHSpeed))
                {
                    bounceSpeed.X += conservedHSpeed;
                }
                conservedHSpeed = 0;
            }
            player.Speed = bounceSpeed;
            //player.Speed *= preBounceSpeed / DashSpeed;

            bool dreamBounced = false;
            bool moveBounced = false;
            foreach (Solid solid in player.CollideAll<Solid>(player.Position + surfaceDir * wallCheckDistance))
            {

                // Dream bounce
                if (solid is DreamBlock && player.Inventory.DreamDash)
                {
                    if (!dreamBounced)
                    {
                        player.Speed *= dreamBounceSpeedMult;
                        player.Play(SFX.char_bad_dreamblock_exit);
                        player.Play(SFX.char_bad_jump_dreamblock);
                        player.Play(SFX.game_gen_crystalheart_bounce);

                        Level level = player.SceneAs<Level>();
                        foreach (ParticleType particle in dreamBounceParticles)
                        {
                            level.Particles.Emit(particle, 2, player.Center + 4 * surfaceDir, Vector2.One * 4, player.Speed.Angle());
                        }
                        dreamBounced = true;
                    }

                    if (dreamRipple)
                    {
                        (solid as DreamBlock).FootstepRipple(player.Position + surfaceDir * WallJumpCheckDist);
                    }
                }

                // Zip mover activation
                if (solid is BounceZipMover zipMopver)
                {
                    if (!zipMopver.moon || player.Holding?.Entity is BounceJellyfish)
                    {
                        zipMopver.activate();
                    }
                }

                // Move block bouncing
                if (solid is BounceMoveBlock moveBlock)
                {
                    float speedMult = moveBlock.bounceImpact(surfaceDir, bounceStrength);
                    if (!moveBounced)
                    {
                        if (surfaceDir.X == 0)
                        {
                            player.Speed.Y *= speedMult;
                        }
                        else
                        {
                            player.Speed.X *= speedMult;
                        }
                        moveBounced = true;
                    }
                }

                // Swap block bouncing
                if (solid is BounceSwapBlock swapBlock)
                {
                    if (swapBlock.moon)
                    {
                        if (player.Holding?.Entity is BounceJellyfish && swapBlock.onBounce(player.Speed.Angle()))
                        {
                            (player.Holding.Entity as BounceJellyfish).refillDash();
                        }
                    }
                    else
                    {
                        if (swapBlock.onBounce(player.Speed.Angle()))
                        {
                            player.RefillDash();
                        }
                    }
                }

                // Falling block activation
                if (solid is BounceFallingBlock fallingBlock)
                {
                    fallingBlock.Trigger();
                }
            }

            playerData.Set("varJumpSpeed", player.Speed.Y);
            playerData.Set("varJumpTimer", SuperWallJumpVarTime);
            playerData.Set("launched", true);
            playerData.Set("gliderBoostTimer", DashGliderBoostTime);
            playerData.Set("gliderBoostDir", Vector2.Normalize(player.Speed));

            // Only creates one slash and trail when corner bouncing
            //if (!(player.DashDir == Vector2.UnitY && surfaceDir.Y == 0 && player.OnGround())) {
            if (!cornerBounced)
            {
                Vector2 slashOffset = Vector2.Normalize(player.Speed) * 12;
                SlashFx.Burst(player.Center + slashOffset, player.Speed.Angle());
                player.Sprite.Scale = Vector2.One;
                Vector2 scale = new Vector2(Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y);
                TrailManager.Add(player, scale, bounceColor);
            }
            else
            {
                if (player.CollideCheck<Solid>(player.Position - surfaceDir))
                {
                    player.Speed.X = 0;
                    holeBounced = true;
                }
            }
        }

        #region Horizontal and diagonal wall bouncing + neutral jump disabling + zip mover activation.
        private void modWallJump(On.Celeste.Player.orig_WallJump orig, Player player, int dir)
        {
            if (isEnabled)
            {
                bool jellyfishBounce = canJellyfishBounce(player);
                if (jellyfishBounce)
                {
                    player.DashDir = jellyfishBounceDir;

                    // Upwards wall jellyfish bouncing
                    if (player.DashDir == -Vector2.UnitY)
                    {
                        playerSuperWallJump.Invoke(player, new object[] { dir });
                        return;
                    }
                }

                orig(player, dir);
                if (player.StateMachine.State == Player.StDash || dreamBounced || jellyfishBounce)
                {
                    Vector2 bounceSpeed;
                    int bounceStrength;
                    if (player.DashDir.X == 0 || player.DashDir.Y == 0)
                    {
                        bounceSpeed = sidewaysBounceSpeed;
                        bounceStrength = perpendicularBounceStrength;
                        player.Play(horizontalBounceSound);

                        if (player.DashDir == Vector2.UnitY && player.OnGround())
                        {
                            cornerBounced = true;
                        }
                    }
                    else
                    {
                        bounceSpeed = diagonalBounceSpeed;
                        bounceStrength = diagonalBounceStrength;
                        player.Play(diagonalBounceSound);
                    }
                    bounceSpeed.X *= dir;
                    Vector2 surfaceDir = new Vector2(-dir, 0);
                    bounce(player, bounceSpeed, bounceStrength, surfaceDir);
                }
                else
                {
                    // Zip mover activation
                    foreach (Solid solid in player.CollideAll<Solid>(player.Position + Vector2.UnitX * -dir * WallJumpCheckDist))
                    {
                        if (solid is BounceZipMover)
                        {
                            BounceZipMover mover = solid as BounceZipMover;
                            if (!mover.moon || player.Holding?.Entity is BounceJellyfish)
                            {
                                mover.activate();
                            }
                        }
                    }

                    jellyfishWallJumpForceTimer = GliderWallJumpForceTime;
                }

                var playerData = getPlayerData(player);
                if (holeBounced)
                {

                    // Fixes hole bouncing weirdness
                    playerData.Set("forceMoveXTimer", 0f);
                    holeBounced = false;
                }
                else
                {

                    // Disabling neutral jump
                    playerData.Set("forceMoveX", dir);
                    float forceMoveXTimer = player.Holding != null && player.Holding.SlowFall ? GliderWallJumpForceTime : WallJumpForceTime;
                    playerData.Set("forceMoveXTimer", forceMoveXTimer);
                }
            }
            else
            {
                orig(player, dir);
            }
        }
        #endregion

        #region Downwards floor bouncing + ceiling bouncing.
        private int modDashUpdate(On.Celeste.Player.orig_DashUpdate orig, Player player)
        {
            int state = orig(player);
            var playerData = getPlayerData(player);
            if (isEnabled && Input.Jump.Pressed && (state == Player.StDash || cornerBounced))
            {
                if (player.DashDir == Vector2.UnitY && (playerData.Get<float>("jumpGraceTimer") > 0 || cornerBounced))
                {
                    downwardBounce(player);
                    jellyfishBounceTimer = 0f;
                    state = Player.StNormal;
                }
                else if (player.DashDir == -Vector2.UnitY && player.CollideCheck<Solid>(player.Position - Vector2.UnitY))
                {
                    ceilingBounce(player);
                    jellyfishBounceTimer = 0f;
                    state = Player.StNormal;
                }
            }
            return state;
        }

        private void downwardBounce(Player player, bool jump = true)
        {
            bool travellingFastHorizontally = Math.Abs(player.Speed.X) > 150f;
            conservedHSpeed = player.Speed.X +
                Math.Sign(travellingFastHorizontally ? player.Speed.X : Input.MoveX) * JumpHBoost;
            if (jump)
            {
                player.Jump();
            }
            else
            {
                Input.Jump.ConsumeBuffer();
            }
            Vector2 bounceSpeed = downwardsBounceSpeed;
            Vector2 surfaceDir = Vector2.UnitY;
            int bounceStrength = perpendicularBounceStrength;
            bool tempCornerBounced = cornerBounced;
            cornerBounced = false;
            bounce(player, bounceSpeed, bounceStrength, surfaceDir, dreamRipple: true);
            player.Play(verticalBounceSound);

            if (player.CanUnDuck)
            {
                player.Ducking = false;
            }

            var playerData = getPlayerData(player);
            if (playerData.Get<float>("dashRefillCooldownTimer") <= 0 && !player.Inventory.NoRefills)
            {
                player.RefillDash();
            }

            // Helps maintain momentum when chaining a sideways bounce into a downwards bounce
            if (!tempCornerBounced && travellingFastHorizontally)
            {
                playerData.Set("forceMoveX", (int)player.Facing);
                playerData.Set("forceMoveXTimer", WallJumpForceTime);
            }
        }

        private void ceilingBounce(Player player)
        {
            Vector2 bounceSpeed = -upwardsBounceSpeed.YComp();
            Vector2 surfaceDir = -Vector2.UnitY;
            int bounceStrength = perpendicularBounceStrength;
            bounce(player, bounceSpeed, bounceStrength, surfaceDir, dreamRipple: true);
            player.Play(verticalBounceSound);
            Input.Jump.ConsumeBuffer();
            Dust.Burst(player.TopCenter, (float)Math.PI / 2f, 4, ParticleTypes.Dust);
        }
        #endregion

        #region Dream block bouncing stuff
        // Main functionality for downward and downward diagonal floor bouncing and ceiling bouncing
        // Improves consistency for all other directions
        private void modDreamDashBegin(On.Celeste.Player.orig_DreamDashBegin orig, Player player)
        {
            if (isEnabled && Input.Jump.Pressed)
            {
                Input.Jump.ConsumeBuffer();
                dreamBounced = true;
                int dashDirX = Math.Sign(player.DashDir.X);
                if (player.DashDir.X == 0)
                {
                    if (player.DashDir.Y > 0)
                    {
                        downwardBounce(player);
                    }
                    else
                    {
                        ceilingBounce(player);
                    }
                    player.StateMachine.State = Player.StNormal;
                }
                else if (player.DashDir.Y == 0 || player.CollideCheck<DreamBlock>(player.Position + Vector2.UnitX * dashDirX))
                {

                    // Sideways or diagonal wall bouncing
                    playerWallJump.Invoke(player, new object[] { -dashDirX });
                    player.StateMachine.State = Player.StNormal;
                }
                else if (player.DashDir.Y > 0)
                {

                    // Diagonal floor bouncing
                    player.Ducking = true;
                    playerSuperJump.Invoke(player, new object[] { });
                    player.StateMachine.State = Player.StNormal;
                    var playerData = getPlayerData(player);
                    if (playerData.Get<float>("dashRefillCooldownTimer") <= 0 && !player.Inventory.NoRefills)
                    {
                        player.RefillDash();
                    }
                }
                else
                {

                    // Prevents diagonal ceiling bouncing
                    orig(player);
                }
            }
            else
            {
                orig(player);
            }
        }

        // Fixes soundsource bug + refills jellyfish dash when dreamdashing while held
        private void modDreamDashEnd(On.Celeste.Player.orig_DreamDashEnd orig, Player player)
        {
            if (dreamBounced)
            {
                dreamBounced = false;
            }
            else
            {
                orig(player);
                if (isEnabled && player.Holding?.Entity is BounceJellyfish)
                {
                    (player.Holding.Entity as BounceJellyfish).refillDash();
                }
            }
        }
        #endregion

        #region Diagonal and horizontal floor bouncing + fixes being able to superjump while in a red booster.
        private void modSuperJump(On.Celeste.Player.orig_SuperJump orig, Player player)
        {
            if (isEnabled)
            {
                if (player.StateMachine.State == Player.StRedDash)
                {
                    player.Jump();
                }
                else
                {
                    bool diagonal = player.Ducking;

                    // Swaps the sound effects to match the swapped resultant velocities.
                    player.Ducking = !player.Ducking;
                    orig(player);

                    Vector2 bounceSpeed;
                    int bounceStrength;
                    if (diagonal)
                    {
                        bounceSpeed = diagonalBounceSpeed;
                        bounceStrength = diagonalBounceStrength;
                    }
                    else
                    {
                        bounceSpeed = sidewaysBounceSpeed;
                        bounceStrength = parallelBounceStrength;
                    }
                    int direction = (int)player.Facing;
                    bounceSpeed.X *= direction;

                    var playerData = getPlayerData(player);
                    Vector2 surfaceDir = Vector2.UnitY;
                    int wallCheckDistance = WallJumpCheckDist;

                    // Fixes dreambouncing when exiting a dream block
                    if (playerData.Get<bool>("dreamJump"))
                    {
                        surfaceDir = Vector2.UnitX * -direction;
                        wallCheckDistance = 20;
                    }
                    bounce(player, bounceSpeed, bounceStrength, surfaceDir, dreamRipple: true, wallCheckDistance);
                }
            }
            else
            {
                orig(player);
            }
        }
        #endregion

        #region Upwards wall bouncing + fixes being able to superwalljump while in a red booster.
        private void modSuperWallJump(On.Celeste.Player.orig_SuperWallJump orig, Player player, int dir)
        {
            if (isEnabled)
            {
                if (player.StateMachine.State == Player.StRedDash)
                {
                    playerWallJump.Invoke(player, new object[] { dir });
                }
                else
                {
                    orig(player, dir);
                    Vector2 bounceSpeed = upwardsBounceSpeed;
                    bounceSpeed.X *= dir;
                    Vector2 surfaceDir = new Vector2(-dir, 0);
                    int bounceStrength = parallelBounceStrength;
                    bounce(player, bounceSpeed, bounceStrength, surfaceDir, dreamRipple: true, wallCheckDistance: 5);
                }
            }
            else
            {
                orig(player, dir);
            }
        }
        #endregion

        #region Jellyfish bouncing stuff
        // Allows jellyfish downwards, downwards diagonal and horizontal floor bouncing
        private void modJump(On.Celeste.Player.orig_Jump orig, Player player, bool particles, bool playSfx)
        {
            if (isEnabled && canJellyfishBounce(player))
            {
                if (jellyfishBounceDir == Vector2.UnitY)
                {
                    bool wallRight = player.CollideCheck<Solid>(player.Position + WallJumpCheckDist * Vector2.UnitX);
                    bool wallLeft = player.CollideCheck<Solid>(player.Position - WallJumpCheckDist * Vector2.UnitX);
                    if (wallRight)
                    {
                        if (!wallLeft)
                        {
                            playerWallJump.Invoke(player, new object[] { -1 });
                        }
                    }
                    else if (wallLeft)
                    {
                        playerWallJump.Invoke(player, new object[] { 1 });
                    }
                    downwardBounce(player, jump: false);
                }
                else
                {
                    if (jellyfishBounceDir.Y > 0)
                    {
                        player.Ducking = true;
                    }
                    playerSuperJump.Invoke(player, new object[] { });
                }
            }
            else
            {
                orig(player, particles, playSfx);
            }
        }

        // Allows upwards jellyfish bouncing + fixes ground burying bug with normal holdables
        private int modNormalUpdate(On.Celeste.Player.orig_NormalUpdate orig, Player player)
        {
            int state = orig(player);
            if (isEnabled)
            {
                if (state == Player.StNormal && Input.Jump.Pressed && canJellyfishBounce(player))
                {
                    if (player.CollideCheck<Solid>(player.Position - Vector2.UnitY))
                    {
                        ceilingBounce(player);
                    }
                }

                // Fixes bug with normal holdables
                var playerData = getPlayerData(player);
                if (!player.OnGround() && player.Holding != null)
                {
                    playerData.Set("holdCannotDuck", (float)Input.MoveY == 1f);
                }
            }
            return state;
        }

        private bool canJellyfishBounce(Player player)
        {
            return jellyfishBounceTimer > 0f && player.Holding?.Entity is BounceJellyfish;
        }

        // Decreases the jellyfishBounceTimer and jellyfishWallJumpTimer, and stops the playing climbing if entering
        // bounce mode while climbing
        private void modUpdate(On.Celeste.Player.orig_Update orig, Player player)
        {
            orig(player);
            if (jellyfishBounceTimer > 0f)
            {
                jellyfishBounceTimer -= Engine.DeltaTime;
            }
            if (jellyfishWallJumpForceTimer > 0f)
            {
                jellyfishWallJumpForceTimer -= Engine.DeltaTime;
            }
            if (isEnabled && player.StateMachine.State == Player.StClimb)
            {
                player.StateMachine.State = Player.StNormal;
            }
        }

        #endregion

        #endregion

        #region Dash direction disabling
        private static Vector2 hiccupBoost = new Vector2(60, -60);
        enum DashResult
        {
            NO_DASH,
            HICCUP,
            DASH
        }

        // Starts from the left, rotates clockwise.
        private DashResult[] dashResults = {
            DashResult.DASH, // Left
            DashResult.DASH, // UpLeft
            DashResult.DASH, // Up
            DashResult.DASH, // UpRight
            DashResult.DASH, // Right
            DashResult.DASH, // DownRight
            DashResult.DASH, // Down
            DashResult.DASH, // DownLeft
        };

        private int modStartDash(On.Celeste.Player.orig_StartDash orig, Player player)
        {
            if (isEnabled)
            {
                var playerData = getPlayerData(player);
                int currState = player.StateMachine.State;
                Vector2 dashDir = playerData.Get<Vector2>("lastAim");
                int dashIndex = ((int)Math.Round(dashDir.Angle() * 180 / Math.PI) + 180) / 45;
                dashIndex = dashIndex == 8 ? 0 : dashIndex;
                DashResult dashResult = dashResults[dashIndex];
                if (dashResult == DashResult.DASH)
                {
                    jellyfishBounceDir = dashDir;
                    jellyfishBounceTimer = DashAttackTime;
                    return orig(player);
                }
                else if (dashResult == DashResult.HICCUP && player.StateMachine.State == Player.StNormal)
                {
                    player.HiccupJump();
                    int moveX = playerData.Get<int>("moveX");
                    player.Speed.X += hiccupBoost.X * moveX;
                    player.Speed.Y += hiccupBoost.Y;
                    player.Dashes = Math.Max(0, player.Dashes - 1);
                }
                Input.Dash.ConsumeBuffer();

                if (Engine.Scene is Level)
                {
                    foreach (BounceJellyfish jellyfish in Engine.Scene.Tracker.GetEntities<BounceJellyfish>())
                    {
                        if (jellyfish.matchPlayerDash)
                        {
                            jellyfish.bufferDash();
                        }
                    }
                }

                return currState;
            }
            else
            {
                return orig(player);
            }
        }
        #endregion

        #region Moving block sliding player attachment
        private bool modIsRiding(On.Celeste.Player.orig_IsRiding_Solid orig, Player player, Solid solid)
        {
            if (isEnabled)
            {
                // Stops being able to trigger ridables by grabbing
                var playerData = getPlayerData(player);
                playerData.Set("climbTriggerDir", 0);

                bool attach = false;
                if (player.Speed.Y >= 0)
                {
                    if (player.CollideCheck(solid, player.Position + new Vector2(Math.Sign(Input.MoveX), 0)))
                    {
                        if (solid is BounceZipMover)
                        {
                            attach = (solid as BounceZipMover).triggered;
                        }

                        if (solid is BounceMoveBlock)
                        {
                            attach = (solid as BounceMoveBlock).triggered;
                        }

                        if (solid is BounceSwapBlock || solid is BounceDreamBlock)
                        {
                            attach = true;
                        }
                    }
                }

                return attach || orig(player, solid);
            }
            else
            {
                return orig(player, solid);
            }
        }
        #endregion

        #region Jellyfish stuff
        private const float downwardThrowRecoil = JumpSpeed;
        private const float maxJellyfishFallSpeedMult = 1.3f;
        private const float jellyfishSlowfallSpeedMult = 40f / 24;

        #region Alters throw behaviour
        // Removes horizontal throw recoil for jellyfish
        // Thrown jellyfish maintains player's momentum
        // Allows full directional throwing
        // Throwing downwards or diagonally downwards will give the player an upwards boost of speed
        private void modThrow(On.Celeste.Player.orig_Throw orig, Player player)
        {
            if (isEnabled && !useVanillaThrowBehaviour && player.Holding != null)
            {
                var playerData = getPlayerData(player);
                Vector2 throwDir = playerData.Get<Vector2>("lastAim");
                if (Input.MoveX.Value == 0 && Input.MoveY.Value == 0 || player.OnGround() && throwDir.Y > 0)
                {
                    player.Drop();
                }
                else
                {
                    if (throwDir.Y > 0)
                    {
                        player.Speed.Y = Math.Min(player.Speed.Y, throwDir.Y * downwardThrowRecoil);
                        playerData.Set("varJumpSpeed", player.Speed.Y);
                        playerData.Set("varJumpTimer", VarJumpTime);
                        player.AutoJump = true;
                        throwDir.Y *= 2f;
                        throwDir.X *= 0.5f;
                    }

                    // Fixes non-jellyfish vertical throw speeds
                    if (!player.Holding.SlowFall)
                    {
                        throwDir.Y *= throwDir.Y < 0 ? 0.8f : 0.4f;
                    }

                    Input.Rumble(RumbleStrength.Strong, RumbleLength.Short);
                    player.Holding.Release(throwDir);
                    player.Play("event:/char/madeline/crystaltheo_throw");
                    player.Sprite.Play("throw");

                    // Removes the ability to create a theocopter with downthrows
                    if (!player.Holding.SlowFall && throwDir.Y > 0)
                    {
                        DynamicData.For(player.Holding).Set("cannotHoldTimer", 0.2f);
                    }
                }
                player.Holding = null;
                jellyfishBounceTimer = 0f;
            }
            else
            {
                orig(player);
            }
        }
        #endregion

        #region Causes jellyfish to die if player dies (if soul bound)
        private PlayerDeadBody modDie(On.Celeste.Player.orig_Die orig, Player player, Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true)
        {
            PlayerDeadBody body = orig(player, direction, evenIfInvincible, registerDeathInStats);
            if (isEnabled && body != null)
            {
                var playerData = getPlayerData(player);
                BounceJellyfish jellyfish = playerData.Get<Level>("level").Tracker.GetEntity<BounceJellyfish>();
                if (jellyfish != null && !jellyfish.destroyed && jellyfish.soulBound)
                {
                    jellyfish.die(playSound: direction != Vector2.Zero);
                }
            }
            return body;
        }
        #endregion

        #region Refills jellyfish dash when the player is holding it and touches the ground
        private bool modRefillDash(On.Celeste.Player.orig_RefillDash orig, Player player)
        {
            if (isEnabled && player.Holding?.Entity is BounceJellyfish && player.OnGround())
            {
                (player.Holding.Entity as BounceJellyfish).refillDash();
            }
            return orig(player);
        }
        #endregion

        #region Refills jellyfish dash when the player is holding it and collides with a spring
        private void modSpringOnCollide(On.Celeste.Spring.orig_OnCollide orig, Spring spring, Player player)
        {
            orig(spring, player);
            if (isEnabled && player.Holding?.Entity is BounceJellyfish)
            {
                (player.Holding.Entity as BounceJellyfish).refillDash();
            }
        }
        #endregion

        #region Alters pickup behaviour
        // Allows downwards momentum to be conserved
        // Eliminates the grab animation wait time
        // Allows jellyfish dashes and bounces to transfer momentum unto the player in the same way that player dashes and bounces can
        private IEnumerator modPickupCoroutine(On.Celeste.Player.orig_PickupCoroutine orig, Player player)
        {
            if (isEnabled && !useVanillaPickupBehaviour)
            {
                var playerData = getPlayerData(player);
                Audio.Play("event:/char/madeline/crystaltheo_lift", player.Position);
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);

                Vector2 begin = player.Holding.Entity.Position - player.Position;
                Vector2 carryOffsetTarget = playerData.Get<Vector2>("CarryOffsetTarget");
                SimpleCurve curve = new SimpleCurve(
                    end: carryOffsetTarget,
                    control: new Vector2(begin.X + (float)(Math.Sign(begin.X) * 2), carryOffsetTarget.Y - 2f),
                    begin: begin
                );
                playerData.Set("carryOffset", begin);
                Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, PickupTime, start: true);
                tween.OnUpdate = delegate (Tween t) {
                    playerData.Set("carryOffset", curve.GetPoint(t.Eased));
                };
                player.Add(tween);
                player.StateMachine.State = 0;

                float gliderBoostTimer = playerData.Get<float>("gliderBoostTimer");
                Vector2 gliderBoostDir = playerData.Get<Vector2>("gliderBoostDir");

                if (player.Holding.SlowFall)
                {
                    bool boostSound = false;
                    if (gliderBoostTimer > 0f)
                    {
                        boostSound = true;
                        if (player.Holding.Entity is BounceJellyfish jellyfish && jellyfish.boostTimer > gliderBoostTimer)
                        {
                            jellyfish.boostTimer = 0f;
                            player.Speed = jellyfish.beforeSpeed;
                            gliderBoostDir = jellyfish.boostDir;
                            jellyfishBounceDir = jellyfish.boostDir;
                            jellyfishBounceTimer = jellyfish.dashAttackTimer;
                        }
                        else
                        {
                            jellyfishBounceDir = gliderBoostDir;
                        }
                        gliderBoostTimer = 0f;

                        if (gliderBoostDir.Y < 0)
                        {
                            player.Speed.Y = Math.Min(player.Speed.Y, -DashSpeed * Math.Abs(gliderBoostDir.Y));
                        }
                        else if (gliderBoostDir.Y == 0)
                        {
                            player.Speed.Y = Math.Min(player.Speed.Y, JumpSpeed);
                        }
                    }
                    else if (player.Holding.Entity is BounceJellyfish jellyfish2 && jellyfish2.boostTimer > 0f)
                    {
                        boostSound = true;
                        jellyfish2.boostTimer = 0f;
                        player.Speed = jellyfish2.beforeSpeed;
                        jellyfishBounceDir = jellyfish2.boostDir;
                        jellyfishBounceTimer = jellyfish2.dashAttackTimer;

                        if (jellyfish2.boostDir.Y < 0)
                        {
                            player.Speed.Y = Math.Min(player.Speed.Y, -DashSpeed * Math.Abs(jellyfish2.boostDir.Y));
                        }
                        else if (jellyfish2.boostDir.Y == 0)
                        {
                            player.Speed.Y = Math.Min(player.Speed.Y, JumpSpeed);
                        }
                    }
                    else
                    {
                        if (player.Speed.Length() > 180f && player.Speed.Y < 0)
                        {
                            boostSound = true;
                        }
                    }

                    if (boostSound)
                    {
                        Audio.Play("event:/Bio/jellyfish_pickup_shortened", player.Position);
                    }
                    if (player.OnGround() && (int)Input.MoveY == 1)
                    {
                        playerData.Set("holdCannotDuck", true);
                    }

                    // Allows jellyfish dash initiated dream dashing
                    if (player.Holding.Entity is BounceJellyfish jellyfish3 &&
                        jellyfish3.dashAttackTimer > playerData.Get<float>("dashAttackTimer")
                    )
                    {
                        playerData.Set("dashAttackTimer", jellyfish3.dashAttackTimer);
                        player.DashDir = jellyfish3.dashDir;
                    }
                }
                float pickupTimeIncrement = 0.007f; // Value tuned such that you can't glitch holdables into walls
                playerData.Set("minHoldTimer", PickupTime + pickupTimeIncrement);

                playerData.Set("forceMoveXTimer", jellyfishWallJumpForceTimer);
                yield break;
            }
            else
            {
                IEnumerator origEnum = orig(player);
                while (origEnum.MoveNext())
                {
                    yield return origEnum.Current;
                }
            }
        }
        #endregion

        #region Increases max fall speed while holding jellyfish and inputting downwards and removes the ability to slowfall by holding up
        private void modNormalUpdate(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            // Jump to where 120f (normal max jellyfish fall speed) is loaded, and replace with new value
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(120f)))
            {
                cursor.EmitDelegate<Func<float>>(() => isEnabled ? maxJellyfishFallSpeedMult : 1);
                cursor.Emit(OpCodes.Mul);
            }

            // Jump to where 24f (jellyfish slowfall speed) is loaded, and replace with new value
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(24f)))
            {
                cursor.EmitDelegate<Func<float>>(() => isEnabled ? jellyfishSlowfallSpeedMult : 1);
                cursor.Emit(OpCodes.Mul);
            }
        }
        #endregion

        #region Allows jellyfish to start dash during player dash game freeze
        private void modEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime gameTime)
        {
            orig(engine, gameTime);
            if (Engine.Scene is Level && Settings.JellyfishDash.Pressed)
            {
                foreach (BounceJellyfish jellyfish in Engine.Scene.Tracker.GetEntities<BounceJellyfish>())
                {
                    if (jellyfish.Active && !jellyfish.matchPlayerDash)
                    {
                        jellyfish.bufferDash();
                    }
                }
            }
        }
        #endregion

        #region Prevents player from leaving the room unless holding the jellyfish (if one exists, and it is soul bound)
        private void modLevelEnforceBounds(On.Celeste.Level.orig_EnforceBounds orig, Level level, Player player)
        {
            BounceJellyfish jellyfish = level.Tracker.GetEntity<BounceJellyfish>();
            if (isEnabled && jellyfish != null && jellyfish.soulBound && player.Holding == null)
            {
                Rectangle bounds = level.Bounds;
                if (player.Right > bounds.Right - 1)
                {
                    player.Right = bounds.Right - 1;
                }
                if (player.Left < bounds.Left - 1)
                {
                    player.Left = bounds.Left - 1;
                }
                if (player.Top < bounds.Top - 1)
                {
                    player.Top = bounds.Top - 1;
                }
                // No enforcing bottom bound?
            }
            orig(level, player);
        }
        #endregion

        #endregion

        #region Speed tech stuff
        private static float preBounceSpeed;

        // Shameless rip of max480's code.
        private void modCallDashEvents(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            // enter the if in the method (the "if" checks if dash events were already called) and inject ourselves in there
            // (those are actually brtrue in the XNA version and brfalse in the FNA version. Seriously?)
            if (cursor.TryGotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Brtrue || instr.OpCode == OpCodes.Brfalse))
            {

                // just add a call to ModifyDashSpeed (arg 0 = this)
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<Player>>(modifyDashSpeed);
            }
        }

        // Downwards dashing horizontal velocity conservation.
        private void modifyDashSpeed(Player player)
        {
            Vector2 beforeDashSpeed = getPlayerData(player).Get<Vector2>("beforeDashSpeed");
            //float inLineSpeed = Vector2.Dot(Vector2.Normalize(player.Speed), beforeDashSpeed);
            //float dashSpeed = player.Speed.Length();

            //if (inLineSpeed > dashSpeed) {
            //    player.Speed *= inLineSpeed / dashSpeed;
            //}

            if (isEnabled && player.DashDir == Vector2.UnitY && player.StateMachine.State == Player.StDash)
            {
                player.Speed.X = beforeDashSpeed.X;
            }
            preBounceSpeed = player.Speed.Length();
        }

        #endregion

        #region Misc
        // Disables climbing
        private bool modClimbCheck(On.Celeste.Player.orig_ClimbCheck orig, Player player, int dir, int yAdd = 0)
        {
            if (isEnabled)
            {
                return false;
            }
            else
            {
                return orig(player, dir, yAdd);
            }
        }

        // Disables climb jumping while not in the normal state.
        private void modClimbJump(On.Celeste.Player.orig_ClimbJump orig, Player player)
        {
            if (isEnabled)
            {
                playerWallJump.Invoke(player, new object[] { -(int)player.Facing });
            }
            else
            {
                orig(player);
            }
        }

        // Fixes wierd bug with getting a vertical boost after cancelling a bounce with a dash
        private void modDashBegin(On.Celeste.Player.orig_DashBegin orig, Player player)
        {
            orig(player);
            if (isEnabled)
            {
                var playerData = getPlayerData(player);
                playerData.Set("varJumpTimer", 0f);
            }
        }

        private void onLevelBegin(On.Celeste.Level.orig_Begin orig, Level level)
        {
            orig(level);
            session = level.Session;
        }

        // Allows displaying the jellyfish dash keybind in the custom Everest bird tutorial
        private object CustomBirdTutorial_OnParseCommand(string command)
        {
            if (command == "BounceHelperJellyfishDash")
            {
                return Settings.JellyfishDash.Button;
            }
            return null;
        }

        private DynamicData getPlayerData(Player player)
        {
            //if (_playerData != null && _playerData.Get<Level>("level") != null) {
            //    return _playerData;
            //}
            return DynamicData.For(player);
        }

        public static void log(string str)
        {
            Logger.Log("Bounce Helper", str);
        }
        #endregion

        #region Replace vanilla entities with BounceHelper equivalents

        // look for vanilla entities and replace them, converting properties if necessary
        private static bool ReplaceVanillaEntities(Level level, LevelData levelData, Vector2 offset, EntityData origEntityData)
        {
            BounceHelperEverywhereSettings settings = Settings.ReplaceVanillaEntities;

            // only clone the EntityData if necessary
            EntityData entityData;

            if (settings.ReplaceBumpers && origEntityData.Name == "bigSpinner")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(Bumper)} at {entityData.Position + offset} with BounceHelper equivalent.");

                // TODO: BounceBumpers don't support nodes
                entityData.Name = "BounceHelper/BounceBumper";
                level.Add(new BounceBumper(entityData, offset));
                return true;
            }
            if (settings.ReplaceDreamBlocks && origEntityData.Name == "dreamBlock")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(DreamBlock)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceDreamBlock";
                if (entityData.FirstNodeNullable() is { } swingNode)
                {
                    // vanilla calculates half of the period, meanwhile oscillationDuration wants the full period
                    // hence the division by 6f instead of 12f
                    float oscillationDuration = Vector2.Distance(entityData.Position, swingNode) / 6f;
                    if (entityData.Bool("fastMoving"))
                        oscillationDuration /= 3f;

                    entityData.Values["oscillationDuration"] = oscillationDuration;
                }

                level.Add(new BounceDreamBlock(entityData, offset));
                return true;
            }
            if (settings.ReplaceFallingBlocks && origEntityData.Name == "fallingBlock")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(FallingBlock)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceFallingBlock";
                level.Add(new BounceFallingBlock(entityData, offset));
                return true;
            }
            if (settings.ReplaceJellyfish && origEntityData.Name == "glider")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(Glider)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceJellyfish";
                entityData.Values["platform"] = entityData.Values["bubble"];
                entityData.Values["soulBound"] = settings.SoulboundJellyfish;
                level.Add(new BounceJellyfish(entityData, offset));
                return true;
            }
            if (settings.ReplaceMoveBlocks && origEntityData.Name == "moveBlock")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(MoveBlock)} at {entityData.Position + offset} with BounceHelper equivalent.");

                // TODO: BounceMoveBlocks don't support steering
                // TODO: BounceMoveBlocks reform after 1s, instead of vanilla's 2.2s
                entityData.Name = "BounceHelper/BounceMoveBlock";
                entityData.Values["speed"] = entityData.Bool("fast") ? 75 : 60;
                level.Add(new BounceMoveBlock(entityData, offset));
                return true;
            }
            if (settings.ReplaceRefills && origEntityData.Name == "refill")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(Refill)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceRefill";
                level.Add(new BounceRefill(entityData, offset));
                return true;
            }
            if (settings.ReplaceSwapBlocks && origEntityData.Name == "swapBlock")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(SwapBlock)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceSwapBlock";
                entityData.Values["moon"] = entityData.Enum<SwapBlock.Themes>("theme") == SwapBlock.Themes.Moon;
                level.Add(new BounceSwapBlock(entityData, offset));
                return true;
            }
            if (settings.ReplaceZipMovers && origEntityData.Name == "zipMover")
            {
                entityData = CloneEntityData(origEntityData);

                Logger.Debug(nameof(BounceHelperModule),
                    $"Replacing {nameof(ZipMover)} at {entityData.Position + offset} with BounceHelper equivalent.");

                entityData.Name = "BounceHelper/BounceZipMover";
                entityData.Values["moon"] = entityData.Enum<ZipMover.Themes>("theme") == ZipMover.Themes.Moon;
                level.Add(new BounceZipMover(entityData, offset));
                return true;
            }

            return false;
        }

        // create an empty Values dict and reuse it instead of making a new one every time
        private static readonly Dictionary<string, object> EmptyValues = [];

        // mutating EntityDatas is bad, because changes persist until the map is reloaded
        // so we clone it instead
        private static EntityData CloneEntityData(EntityData entityData) => new() {
            ID = entityData.ID,
            Height = entityData.Height,
            Level = entityData.Level,
            Name = entityData.Name,
            Nodes = [..entityData.Nodes], // this syntax requires the .net 8 sdk to build
            Origin = entityData.Origin,
            Position = entityData.Position,
            Values = new Dictionary<string, object>(entityData.Values ?? EmptyValues), // apparently Values can be null
            Width = entityData.Width,
        };

        #endregion
    }
}
