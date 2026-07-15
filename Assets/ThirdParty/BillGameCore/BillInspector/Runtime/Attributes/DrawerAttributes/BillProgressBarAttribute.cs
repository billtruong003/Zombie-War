using System;

namespace BillInspector
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BillProgressBarAttribute : BillDrawerAttribute
    {
        public float MinValue { get; }
        public float MaxValue { get; }
        public ColorType Color { get; }
        public string MaxValueExpression { get; }

        public BillProgressBarAttribute(float minValue, float maxValue, ColorType color = ColorType.Blue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            Color = color;
        }

        /// <summary>Dynamic max from field or expression.</summary>
        public BillProgressBarAttribute(float minValue, string maxValueExpression, ColorType color = ColorType.Blue)
        {
            MinValue = minValue;
            MaxValueExpression = maxValueExpression;
            Color = color;
        }
    }
}
