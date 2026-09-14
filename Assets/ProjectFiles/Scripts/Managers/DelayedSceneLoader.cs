using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
namespace VerifyingOhmsLaw
{
    public class DelayedSceneLoader : MonoBehaviour
    {
        [Header("Scene Settings")]
        public string sceneName;        // Scene name to load
        public float delaySeconds = 3f; // ⏱ Public delay time

        bool isLoading = false;

        // 🔘 Call this from Button
        public void LoadSceneWithDelay()
        {
            if (isLoading) return;
            StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            isLoading = true;

            yield return new WaitForSeconds(delaySeconds);

            SceneManager.LoadScene(sceneName);
        }
    }
}