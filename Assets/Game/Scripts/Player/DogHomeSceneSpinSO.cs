using System.Collections.Generic;
using UnityEngine;

namespace DogSimulator.UI
{
    [CreateAssetMenu(menuName = "Dog Simulator/Dog HomeScene Spin")]
    public sealed class DogHomeSceneSpinSO : ScriptableObject
    {
        [SerializeField] private List<Sprite> dogSpinList;

        public Sprite GetRandomDog()
        {
            if (dogSpinList == null || dogSpinList.Count == 0) return null;

            var start = Random.Range(0, dogSpinList.Count);
            for (var offset = 0; offset < dogSpinList.Count; offset++)
            {
                var sprite = dogSpinList[(start + offset) % dogSpinList.Count];
                if (sprite != null) return sprite;
            }

            return null;
        }
    }
}
