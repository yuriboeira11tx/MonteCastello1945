using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FEB.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SceneLoadingScreen : MonoBehaviour
    {
        [Serializable]
        sealed class Card
        {
            public string title;
            public Texture2D portrait;
            [TextArea(2, 4)] public string fact;
            [TextArea(1, 3)] public string source;
        }

        const string LoadingScene = "Carregamento";
        [SerializeField] string startupDestination = "Menu";
        [SerializeField, Min(0)] float minimumSeconds = 4f;
        [SerializeField] Card[] cards = Array.Empty<Card>();

        static string pendingDestination;
        static int[] order;
        static int remaining;
        static int lastCard;
        LoadingScreenView view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            pendingDestination = null;
            order = null;
            remaining = 0;
            lastCard = -1;
        }

        public static void Load(string destination)
        {
            if (pendingDestination != null) return;
            if (destination == LoadingScene || !Application.CanStreamedLevelBeLoaded(destination))
                throw new ArgumentException("Cena de destino indisponível: " + destination, nameof(destination));

            SceneManager.LoadSceneAsync(LoadingScene, LoadSceneMode.Single);
            pendingDestination = destination;
        }

        IEnumerator Start()
        {
            string destination = pendingDestination ?? startupDestination;
            pendingDestination = destination;
            if (cards.Length > 0)
            {
                var card = cards[DrawCard(cards.Length)];
                view = new LoadingScreenView(GetComponent<UIDocument>().rootVisualElement);
                view.Show(card.portrait, card.fact);
            }

            // Give the panel its first frame before the engine starts integrating scene assets.
            yield return null;
            double earliestActivation = Time.realtimeSinceStartupAsDouble + minimumSeconds;
            var operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            while (operation.progress < .9f || Time.realtimeSinceStartupAsDouble < earliestActivation)
                yield return null;

            pendingDestination = null;
            operation.allowSceneActivation = true;
        }

        static int DrawCard(int count)
        {
            if (order == null || order.Length != count)
            {
                order = new int[count];
                remaining = 0;
            }
            if (remaining == 0)
            {
                for (int i = 0; i < count; i++) order[i] = i;
                for (int i = count - 1; i > 0; i--)
                {
                    int other = UnityEngine.Random.Range(0, i + 1);
                    (order[i], order[other]) = (order[other], order[i]);
                }
                if (count > 1 && order[count - 1] == lastCard)
                    (order[0], order[count - 1]) = (order[count - 1], order[0]);
                remaining = count;
            }
            lastCard = order[--remaining];
            return lastCard;
        }

        void OnDestroy() => view?.Dispose();
    }
}
