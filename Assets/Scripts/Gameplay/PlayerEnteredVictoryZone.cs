using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using UnityEditor;


namespace Platformer.Gameplay
{

    /// <summary>
    /// This event is triggered when the player character enters a trigger with a VictoryZone component.
    /// </summary>
    /// <typeparam name="PlayerEnteredVictoryZone"></typeparam>
    public class PlayerEnteredVictoryZone : Simulation.Event<PlayerEnteredVictoryZone>
    {
        public VictoryZone victoryZone;

        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var finalScore = RealmController.PlayerWon();
            var didClickRestart = EditorUtility.DisplayDialog("You won!", $"Final Score = {finalScore}", "restart game");
            if (didClickRestart == true)
            {
                Simulation.Schedule<PlayerSpawn>(2);
                RealmController.RestartGame();
            }
        }
    }
}