using Game.Services;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Battle scene entry. Presents the current battle (step 13 BattleView) and records the result, then returns to Main.
    /// Without a presenter the battle is resolved instantly (same outcome as skip).
    /// </summary>
    public sealed class BattleSceneRoot : MonoBehaviour
    {
        /// <summary>Optional presenter installed by Game.BattleView; returns when the player leaves the battle.</summary>
        public static System.Func<BattleSceneRoot, ActiveBattle, System.Collections.IEnumerator> Presenter;

        private void Start()
        {
            if (!AppContext.IsBooted) return;
            var session = AppContext.Services.Get<GameSession>();
            var battle = session.Battles.Current;
            if (battle == null)
            {
                ReturnToMain(null);
                return;
            }
            if (Presenter != null) StartCoroutine(Run(battle));
            else Resolve(skip: true);
        }

        private System.Collections.IEnumerator Run(ActiveBattle battle)
        {
            yield return Presenter(this, battle);
        }

        /// <summary>Records the battle (finishing remaining turns instantly) and goes back to Main.</summary>
        public void Resolve(bool skip, bool retreat = false)
        {
            var session = AppContext.Services.Get<GameSession>();
            var result = retreat ? session.Battles.Retreat() : session.Battles.Finish();
            if (!result.Ok)
            {
                Debug.LogError("[Battle] could not record the battle: " + result);
                session.Battles.Discard();
            }
            ReturnToMain(result.Ok ? result.Value : null);
        }

        private static void ReturnToMain(FightCompletion completion)
        {
            AppContext.Services.Get<BattleHandoff>().Completion = completion;
            AppContext.Services.Get<SceneLoader>().Load(SceneNames.Main);
        }
    }
}
