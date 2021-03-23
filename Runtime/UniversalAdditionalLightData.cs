namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Contains extension methods for Light class.
    /// </summary>
    public static class LightExtensions
    {
        /// <summary>
        /// Universal Render Pipeline exposes additional rendering data in a separate component.
        /// This method returns the additional data component for the given light or create one if it doesn't exists yet.
        /// </summary>
        /// <param name="camera"></param>
        /// <returns>The <c>UniversalAdditionalLightData</c> for this light.</returns>
        /// <see cref="UniversalAdditionalLightData"/>
        public static UniversalAdditionalLightData GetUniversalAdditionalLightData(this Light light)
        {
            var gameObject = light.gameObject;
            bool componentExists = gameObject.TryGetComponent<UniversalAdditionalLightData>(out var lightData);
            if (!componentExists)
                lightData = gameObject.AddComponent<UniversalAdditionalLightData>();

            return lightData;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class UniversalAdditionalLightData : MonoBehaviour
    {
        [Tooltip("柔和程度")]
        [SerializeField] float _softness = 0.0f;

        [Tooltip("光源颜色混合权重")]
        [SerializeField] float _weight = 1.0f;

        [Tooltip("控制光源衰减开始距离")]
        [SerializeField] float _attenuationBulbSize = 0.5f;

        [Tooltip("控制阴影是否使用管线设置")]
        [SerializeField] bool _usePipelineSettings = true;

        public float softness
        {
            get { return _softness; }
            set { _softness = value; }
        }

        public float weight
        {
            get { return _weight; }
            set { _weight = value; }
        }

        public float attenuationBulbSize
        {
            get { return _attenuationBulbSize; }
            set { _attenuationBulbSize = value; }
        }

        public bool usePipelineSettings
        {
            get { return _usePipelineSettings; }
            set { _usePipelineSettings = value; }
        }
    }
}