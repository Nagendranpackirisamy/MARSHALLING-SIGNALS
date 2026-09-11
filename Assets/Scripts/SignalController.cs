using UnityEngine;

public class SignalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;
    [SerializeField] private LightGunController lightGun;
    [SerializeField] private FlareController flareController;

    private void OnEnable()
    {
        scenarioManager.OnScenarioLoaded += HandleScenarioLoaded;
    }

    private void OnDisable()
    {
        scenarioManager.OnScenarioLoaded -= HandleScenarioLoaded;
    }

    private void HandleScenarioLoaded(ScenarioData scenario, int index)
    {
        if (lightGun != null) lightGun.ClearSignal();

        switch (scenario.signalVisualType)
        {
            case SignalVisualType.LightGun:
                lightGun?.SetSignal(scenario.lightColor, scenario.lightPattern);
                break;

            case SignalVisualType.AlternatingRedGreen:
                lightGun?.PlayAlternatingSignal();
                break;

            case SignalVisualType.Flare:
                flareController?.PlayFlare();
                break;
        }
    }
}