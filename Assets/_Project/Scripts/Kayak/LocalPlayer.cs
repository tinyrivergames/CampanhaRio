using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// Marks the kayak this machine's player controls. The camera, the HUD and the device input follow it.
    /// Exactly one kayak should have it; bots and (in Etapa 8) remote players don't.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KayakController))]
    public class LocalPlayer : MonoBehaviour
    {
        void OnEnable() => KayakRegistry.SetLocal(GetComponent<KayakController>());

        void OnDisable()
        {
            if (KayakRegistry.Local == GetComponent<KayakController>()) KayakRegistry.SetLocal(null);
        }
    }
}
