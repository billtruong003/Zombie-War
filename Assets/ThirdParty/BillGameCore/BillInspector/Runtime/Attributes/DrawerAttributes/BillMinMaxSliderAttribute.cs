using System;

namespace BillInspector
{
    /// <summary>
    /// Dual-handle slider for Vector2. X = min, Y = max.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class BillMinMaxSliderAttribute : BillDrawerAttribute
    {
        public float MinLimit { get; }
        public float MaxLimit { get; }

        public BillMinMaxSliderAttribute(float minLimit, float maxLimit)
        {
            MinLimit = minLimit;
            MaxLimit = maxLimit;
        }
    }
}
