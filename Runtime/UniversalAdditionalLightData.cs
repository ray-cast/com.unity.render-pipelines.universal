namespace UnityEngine.Rendering.Universal
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class UniversalAdditionalLightData : MonoBehaviour
    {
        [Tooltip("Controls the usage of pipeline settings.")]
        [SerializeField] bool _usePipelineSettings = true;

        public bool usePipelineSettings
        {
            get { return _usePipelineSettings; }
            set { _usePipelineSettings = value; }
        }
    }
}