using System;
using System.Collections.Generic;
using System.Threading;
using Dora.Robot;
using UnityEngine;
using UnityEngine.UI;

namespace Dora
{
    public class Simulator : MonoBehaviour
    {
        public SimulationConfiguration SimConfig = new SimulationConfiguration(); // Should this be a struct?!
        private SimulationPlayState _playState = SimulationPlayState.Paused;

        public Transform simulationContainer;

        public RobotForceController simulationUnit;
        public RobotForceController simulationUnit2;
        public Text Text;
        private int _physicsTickCount = 0;
        private int _robotLogicTickCount = 0;
        private int _physicsTicksSinceUpdate = 0;
        private int _simulatedTimeMillis = 0;
        

        public SimulationPlayState PlayState { get; }

        public SimulationPlayState AttemptSetPlayState(SimulationPlayState targetState)
        {
            if (targetState == _playState) return _playState;
            // TODO: Check if possible to change (For example, not possible if no map is generated)
            _playState = targetState;
            // Reset next update time when changing play mode to avoid skipping ahead
            _nextUpdateTimeMillis = Utils.CurrentTimeMillis();
            return _playState;
        }

        private void Start()
        {
            Physics.autoSimulation = false;
        }

        // Timing variables for controlling the simulation in a manner that is decoupled from Unity's update system
        private long _nextUpdateTimeMillis = 0;

        private void FixedUpdate()
        {
            if (_playState == SimulationPlayState.Paused) return;

            long startTimeMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            int millisPerFixedUpdate = (int) (1000f * Time.fixedDeltaTime);
            // Subtract 5 milliseconds to allow for other procedures such as rendering to occur between updates 
            millisPerFixedUpdate -= 5;
            long fixedUpdateEndTime = startTimeMillis + millisPerFixedUpdate;

            // ReSharper disable once PossibleLossOfFraction
            int physicsTickDeltaMillis = SimConfig.LogicTickDeltaMillis / SimConfig.PhysicsTicksPerLogicUpdate;

            // Only calculate updates if there is still time left in the current update
            while (Utils.CurrentTimeMillis() - startTimeMillis < millisPerFixedUpdate)
            {
                // Yield if no more updates are needed this FixedUpdate cycle
                if (_nextUpdateTimeMillis > fixedUpdateEndTime) break;

                UpdateSimulation(physicsTickDeltaMillis, SimConfig);
                
                // The delay before simulating the next update is dependant on the current simulation play speed
                int updateDelayMillis = physicsTickDeltaMillis / (int) _playState;
                _nextUpdateTimeMillis = _nextUpdateTimeMillis + updateDelayMillis;
                // Do not try to catch up if more than 0.5 seconds behind (higher if tick delta is high)
                long maxDelayMillis = Math.Max(500, physicsTickDeltaMillis * 10);
                _nextUpdateTimeMillis = Math.Max(_nextUpdateTimeMillis, Utils.CurrentTimeMillis() - maxDelayMillis);
            }
            var simulatedTimeSpan = TimeSpan.FromMilliseconds(_simulatedTimeMillis);
            var output = simulatedTimeSpan.ToString(@"hh\:mm\:ss");
            Text.text = "Phys. ticks: " + _physicsTickCount + 
                        "\nLogic ticks: " + _robotLogicTickCount + 
                        "\nSimulated: " + output;
        }

        private void UpdateSimulation(int physicsTickDeltaMillis, SimulationConfiguration config)
        {
            List<SimulationUnit> simUnits = new List<SimulationUnit>();

            foreach (Transform child in simulationContainer)
            {
                SimulationUnit unit = child.GetComponent<SimulationUnit>();
                if (unit != null) simUnits.Add(unit);
            }

            simUnits.ForEach(simUnit => simUnit.PhysicsUpdate(config));
            
            float physicsTickDeltaSeconds = physicsTickDeltaMillis / 1000.0f;
            Physics.Simulate(physicsTickDeltaSeconds);
            _physicsTickCount += 1; 
            _simulatedTimeMillis += physicsTickDeltaMillis;
            _physicsTicksSinceUpdate++;
            if (_physicsTicksSinceUpdate >= SimConfig.PhysicsTicksPerLogicUpdate)
            {
                simUnits.ForEach(simUnit => simUnit.LogicUpdate(config));
                _physicsTicksSinceUpdate = 0;
                _robotLogicTickCount += 1;
            }
        }
    }
}