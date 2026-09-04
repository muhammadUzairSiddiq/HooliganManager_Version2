using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Pastoral.Partical
{
    public class ParticalAnimation : MonoBehaviour
    {
        [SerializeField] private GameObject ParticalPrefab;
        private List<GameObject> particalPool = new List<GameObject>();
        private int maxParticals = 100;

        private ParticalAnimation() { }

        public static ParticalAnimation Instance;
        
        void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public void AnimatieParticalsTo(GameObject particalPrefab, int numberOfItems)
        {
            InitializeParticalPool(particalPrefab, numberOfItems);
        }

        private void InitializeParticalPool(GameObject particalPrefab, int numberOfItems)
        {
            int currentPoolSize = particalPool.Count;
            int additionalParticalsNeeded = Mathf.Min(numberOfItems, maxParticals) - currentPoolSize;

            for (int i = 0; i < additionalParticalsNeeded; i++)
            {
                GameObject partical = Instantiate(particalPrefab, transform);
                partical.SetActive(false);
                particalPool.Add(partical);
            }
        }

        public List<GameObject> GetParticals(GameObject particalPrefab, int numberOfItems)
        {
            List<GameObject> particals = new List<GameObject>();

            for (int i = 0; i < numberOfItems; i++)
            {
                if (particalPool.Count > 0)
                {
                    GameObject partical = particalPool[0];
                    particalPool.RemoveAt(0);
                    particals.Add(partical);
                }
                else
                {
                    GameObject partical = Instantiate(particalPrefab, transform);
                    partical.SetActive(false);
                    particals.Add(partical);
                }
            }

            return particals;
        }

        public IEnumerator MovePartical(GameObject partical, Vector3 startPosition, Vector3 endPosition, float duration)
        {
            float elapsedTime = 0;
            partical.transform.position = startPosition;
            partical.SetActive(true);

            while (elapsedTime < duration)
            {
                partical.transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            partical.transform.position = endPosition;
            partical.SetActive(false);
            particalPool.Add(partical); // Return the partical to the pool
        }

        public void MoveParticalsWithOffset(int numberOfItems, Vector3 fromPosition, Vector3 toPosition, Vector3 offset, float duration)
        {
            List<GameObject> particals = GetParticals(ParticalPrefab, numberOfItems);

            foreach (GameObject partical in particals)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-offset.x, offset.x),
                    Random.Range(-offset.y, offset.y),
                    Random.Range(-offset.z, offset.z)
                );

                Vector3 startPosition = fromPosition + randomOffset;
                StartCoroutine(MovePartical(partical, startPosition, toPosition, duration));
            }
        }
    }
}