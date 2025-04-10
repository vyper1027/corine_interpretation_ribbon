namespace ArcGISTest
{
    public class ArcGISTestClass : Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute
    {
        public ArcGISTestClass(string productId = "ArcGISPro") : base()
        {
            // Install domain wide assembly resolver
            TestResolver.Install(productId);
        }
    }
}