using System;
using System.Collections.Generic;
using System.Xml;
using Xunit;
using Domain.Modes.ClassicSetsMode;
using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;

namespace Domain.Tests
{
    public class ClassicSetsModeTests
    {
        private static (Game game, ClassicSetsMode mode, Player p1, Player p2, ClassicSetsPlayerScore s1, ClassicSetsPlayerScore s2, Dictionary<Guid, PlayerScore> allScores)
            Setup
            (int scorePerLeg = 201,
            bool doubleOutEnabled = false,
            bool suddenDeathEnabled = false,
            int setsToWinMatch = 3,
            int suddenDeathWinningLeg = 6)
        {
            var settings = new ClassicSetsModeSettings(
                scorePerLeg: scorePerLeg,
                doubleOutEnabled: doubleOutEnabled,
                suddenDeathEnabled: suddenDeathEnabled,
                setsToWinMatch: setsToWinMatch,
                suddenDeathWinningLeg: suddenDeathWinningLeg
            );

            var mode = new ClassicSetsMode(settings);

            var p1 = new Player("P_1");
            var p2 = new Player("P_2");
            var players = new List<Player> { p1, p2 };

            var s1 = (ClassicSetsPlayerScore)mode.CreateInitialScore(p1.Id);
            var s2 = (ClassicSetsPlayerScore)mode.CreateInitialScore(p2.Id);
            var allScores = new Dictionary<Guid, PlayerScore>
            {
                [p1.Id] = s1,
                [p2.Id] = s2
            };
            
            var game = new Game(mode, players);

            return (game, mode, p1, p2, s1, s2, allScores);
        }

        [Fact]
        public void Bust_when_negative_score()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 50, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_leaving_one_in_doubleout_mode()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 52, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_zero_but_not_double_in_doubleout_mode()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Normal_subtraction_decreases_remaining()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            var dart = new ThrowData(20, 3);

            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201 - 60, updated.RemainingInLeg);
        }

        [Fact]
        public void Win_leg_single_when_double_out_disabled()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_leg_double_when_double_out_enabled()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(10, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_set_resets_other_player_leg_counters_and_increments_sets()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 0 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            // Set won, match not yet -> Continue with otherUpdatedStates present
            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player.
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(1, updated.SetsWonInMatch);
            // Second player.
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(0, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Win_match_normal_flow()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            // player close to match: sets = 2 and legsWonInSet = 2 -> win leg leads to set and match
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

            var dart = new ThrowData(20, 1); // finishes leg -> set -> match
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(10, otherUpdated.RemainingInLeg);
            Assert.Equal(1, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }
        
        // Minimum 2 legs ahead required to win decider.
        [Fact]
        public void Decider_continues_despite_legs_to_win_match_reached()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(suddenDeathEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(3, updated.LegsWonInSet);
            Assert.Equal(2, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_two_legs_in_sudden_death()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(suddenDeathEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 3, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(30, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_sudden_death_last_leg()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(suddenDeathEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 6, LegsWonInSet = 5, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 20, LegsWonInSet = 5, SetsWonInMatch = 2 };

            var dart = new ThrowData(3, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(20, otherUpdated.RemainingInLeg);
            Assert.Equal(5, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Workflow_tests()
        {
            var (game, mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled:true);

            // P1 turn (starts the set)
            var r1 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r2 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r3 = game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P1 Should not be able to throw
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            // P2 Should be able to throw
            var r4 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r5 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r6 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:0:21
            // P1 turn
            var r7 = game.RegisterThrow(p1.Id, new ThrowData(7, 3)); // (P1) 0:0:21 -> bust
            // P2 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            var r8 = game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            var r9 = game.RegisterThrow(p2.Id, new ThrowData(10, 2)); // (P2) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r9.Proggress);
            // P2 turn (starts a leg).
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            var r10 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r11 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r12 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:1:21
            // P1 turn
            var r13 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r14 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r15 = game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            var r16 = game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            var r17 = game.RegisterThrow(p2.Id, new ThrowData(10, 2)); // (P2) 0:2:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r17.Proggress);
            // P1 turn (starts the leg)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            var r18 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r19 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r20 = game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            var r21 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r22 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r23 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            var r24 = game.RegisterThrow(p1.Id, new ThrowData(7, 1));
            var r25 = game.RegisterThrow(p1.Id, new ThrowData(7, 2)); // (P1) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r25.Proggress);
            // P2 turn (starts the leg)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            var r26 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r27 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r28 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn
            var r29 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r30 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r31 = game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:1:21
            // P2 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            var r32 = game.RegisterThrow(p2.Id, new ThrowData(20, 1)); // (P2) 0:2:21 -> bust
            // P1 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            var r33 = game.RegisterThrow(p1.Id, new ThrowData(1, 1));
            var r34 = game.RegisterThrow(p1.Id, new ThrowData(10, 2)); // (P1) 0:2:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r34.Proggress);

            // Score states check
            var p1Score = Assert.IsType<ClassicSetsPlayerScore>(game.ScoreStates[p1.Id]);
            var p2Score = Assert.IsType<ClassicSetsPlayerScore>(game.ScoreStates[p2.Id]);
            // P1
            Assert.Equal(201, p1Score.RemainingInLeg);
            Assert.Equal(2, p1Score.LegsWonInSet);
            Assert.Equal(0, p1Score.SetsWonInMatch);
            // P2
            Assert.Equal(201, p2Score.RemainingInLeg);
            Assert.Equal(2, p2Score.LegsWonInSet);
            Assert.Equal(0, p2Score.SetsWonInMatch);

            // P1 turn (starts the leg)
            var r35 = game.RegisterThrow(p1.Id, new ThrowData(19, 3));
            var r36 = game.RegisterThrow(p1.Id, new ThrowData(19, 3));
            var r37 = game.RegisterThrow(p1.Id, new ThrowData(19, 3)); // (P1) 0:2:30
            // P2 turn (starts the leg)
            var r38 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r39 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r40 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn (starts the leg)
            var r41 = game.RegisterThrow(p1.Id, new ThrowData(10, 2));
            var r42 = game.RegisterThrow(p1.Id, new ThrowData(5, 1));
            var r43 = game.RegisterThrow(p1.Id, new ThrowData(20, 1)); // (P1) 0:2:30 -> bust
            // P2 turn (starts the leg)
            var r44 = game.RegisterThrow(p2.Id, new ThrowData(5, 1));
            var r45 = game.RegisterThrow(p2.Id, new ThrowData(8, 2)); // (P2) 1:0:201 -> set won
            Assert.Equal(ProggressInfo.SetWon, r45.Proggress);
            // P2 turn (starts the set)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            var r46 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r47 = game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            var r48 = game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 1:0:21
            // P1 turn (starts the leg)
            var r49 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r50 = game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            var r51 = game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            var r52 = game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            var r53 = game.RegisterThrow(p2.Id, new ThrowData(10, 1));
            var r54 = game.RegisterThrow(p2.Id, new ThrowData(0, 1)); // (P2) 1:0:21
            // P1 turn (starts the leg)
            var r55 = game.RegisterThrow(p1.Id, new ThrowData(1, 1));
            var r56 = game.RegisterThrow(p1.Id, new ThrowData(10, 2)); // (P1) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r56.Proggress);
            
            // Score states check
            p1Score = Assert.IsType<ClassicSetsPlayerScore>(game.ScoreStates[p1.Id]);
            p2Score = Assert.IsType<ClassicSetsPlayerScore>(game.ScoreStates[p2.Id]);
            // P1
            Assert.Equal(201, p1Score.RemainingInLeg);
            Assert.Equal(1, p1Score.LegsWonInSet);
            Assert.Equal(0, p1Score.SetsWonInMatch);
            // P2
            Assert.Equal(201, p2Score.RemainingInLeg);
            Assert.Equal(0, p2Score.LegsWonInSet);
            Assert.Equal(1, p2Score.SetsWonInMatch);
        }
    }
}