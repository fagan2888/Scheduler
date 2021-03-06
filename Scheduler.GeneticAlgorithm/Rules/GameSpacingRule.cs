﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scheduler.Domain;

namespace Scheduler.GeneticAlgorithm.Rules
{
    /// <summary>
    /// A rule that penalizes seasons where teams repeat games prior to playing 
    /// all other opponents.
    /// 
    /// The penalty is based on how close in proximity the repeat games are.
    /// </summary>
    /// <remarks>
    /// We don't need to penalize for repeat games that are too distant, because each
    /// opponent in that category is balanced by an opponent that is played too soon.
    /// </remarks>
    public class GameSpacingRule : IRule
    {
        private int optimialMinimumSpaceBetweenOpponents = 0;
        private readonly List<string> teamNames = new List<string>();
        private readonly List<int> penaltyMatrix = new List<int>();

        public void Initialize(League league)
        {
            this.optimialMinimumSpaceBetweenOpponents = league.Teams.Count - 1;

            // Store the teams for quick access later
            this.teamNames.AddRange(league.Teams.Select(t => t.Name));

            // Penalize close repetitions harsher than those that are nearly optimal
            // The index represents the number of games distant from the optimal that the
            // match-up is.
            for (int i = 0; i < this.optimialMinimumSpaceBetweenOpponents; i++)
            {
                // Not much penalty for one game off, a tonne for 5+ games off
                this.penaltyMatrix.Add(i * -2 + Math.Max(0, (i - 1)) * -5 + Math.Max(0, (i - 2)) * -40);
            }

            // For an 8 team league, the penalty matrix will be:
            // 0, 2, 9, 56, 103, 150, 197
        }
          
        public int Apply(Season season)
        {
            return this.InnerApply(season, null);
        }

        private int InnerApply(Season season, List<RuleMessage> messages) 
        {
            int totalPenalty = 0;

            // Store an ordered list of opponents for each team.
            var opponentDictionary = new Dictionary<string, List<string>>();
            this.teamNames.ForEach(t => opponentDictionary.Add(t, new List<string>()));

            foreach (var week in season.Weeks) 
            {
                foreach (var game in week.Games)
                {
                    opponentDictionary[game.Home.Name].Add(game.Away.Name);
                    opponentDictionary[game.Away.Name].Add(game.Home.Name);
                }
            }

            // Now for each team, penalize repetitions that happen too early based on the penalty matrix
            var alreadyProcessed = new List<string>(); // the teams we've already dealt with
            foreach (var team in opponentDictionary.Keys)
            {
                var opponentSchedule = opponentDictionary[team];
                foreach (var opponent in this.teamNames.Where(t => t != team))
                {
                    // Don't double-process match-ups
                    if (alreadyProcessed.Contains(opponent)) continue;

                    var indecies = opponentSchedule.FindIndecies(opponent);
                    for (int i = 0; i < indecies.Count - 1; i++) 
                    {
                        int penaltyIndex = this.optimialMinimumSpaceBetweenOpponents - (indecies[i + 1] - indecies[i]);
                        if (penaltyIndex >= 0 && penaltyIndex < this.penaltyMatrix.Count)
                        {
                            totalPenalty += this.penaltyMatrix[penaltyIndex];
                            if (messages != null && this.penaltyMatrix[penaltyIndex] != 0) messages.Add(new RuleMessage(this.penaltyMatrix[penaltyIndex], string.Format("{0} plays {1} on week {2} and then week {3}", team, opponent, indecies[i] + 1, indecies[i + 1] + 1)));
                        }
                    }
                }
                
                alreadyProcessed.Add(team);
            }
            
            return totalPenalty;
        }

        public List<RuleMessage> Report(Season season)
        {
            var messages = new List<RuleMessage>();
            InnerApply(season, messages);
            return messages;
        }
    }
}
