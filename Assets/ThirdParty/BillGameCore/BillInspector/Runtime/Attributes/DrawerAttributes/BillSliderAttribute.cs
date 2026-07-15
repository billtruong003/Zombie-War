using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BillSliderAttribute : BillDrawerAttribute
    {
        public float MinValue { get; }
        public float MaxValue { get; }

        public BillSliderAttribute(float minValue, float maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }
}
