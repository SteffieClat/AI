using System;
using AIGame.Core;
using UnityEngine;

namespace Bobbily
{
    /// <summary>
    /// Factory that spawns BobbilyAgentFactory agents.
    /// Creates a full team of agents using custom AI behaviour.
    /// </summary>
    [RegisterFactory("BobbilyAgentFactory")]
    public class BobbilyAgentFactory : AgentFactory
    {
        /// <summary>
        /// Returns the agent types this factory wants to spawn.
        /// </summary>
        /// <returns>An array containing the AI types to spawn.</returns>
        protected override System.Type[] GetAgentTypes()
        {
            return new System.Type[] { typeof(BobbilyAgent) };
        }
    }
}