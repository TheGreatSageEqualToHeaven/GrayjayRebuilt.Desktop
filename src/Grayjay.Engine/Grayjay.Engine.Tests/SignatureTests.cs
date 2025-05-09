using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class SignatureTests
    {
        private const string Tag = "SignatureTests";

        [TestMethod]
        public void RoundtripTest()
        {
            var keys = SignatureProvider.GenerateKeyPair();
            Console.WriteLine($"{Tag}: public key: {keys.PublicKey}\nprivate key: {keys.PrivateKey}");

            var signature = SignatureProvider.Sign("test", keys.PrivateKey);
            Assert.IsTrue(SignatureProvider.Verify("test", signature, keys.PublicKey), "Signature verification failed.");
        }

        [TestMethod]
        public void DecodeTest()
        {
            Assert.IsTrue(
                SignatureProvider.Verify(
                    "//this is just an empty script",
                    "eLdlDIcmpTQmfpCumB5NQwFa0ZDNU8hkRB12/Lg+CdTwPrfTIylGeN6jpTmJrEivyLjj" +
                    "5qHWZeNmrHP++9XFwfwzcaXNspKU9YrL3+Bsy2WNnXfQDeB2t4AkzWYAEfm8/kEcK0Ov8dzy0KW" +
                    "lJsxmW+Oj3mFNVP6PV5ZQY1Gju6W8Jw0sGCxnbuhswtRDPwBKnZQUhlZEXPvbrcblW1q5fCESnf" +
                    "oiJ2MHR5epgHfAuMsoY9EAHVXuyrLvmbWADeVwC5jvWLAkJKw68rQmARqV5BBWkpqFEBQcg50CR" +
                    "vTXtPr8IDjW7yiJ6x9nTG3nokTJn3fj2D3hBEHttEG+KhTMlQ==",
                    "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAucY14D6cl0fK5fHOTUfKMz1iQmfJMg" +
                    "Q+c4MfqlArGCv7YDTvazeQL9dsrCqlYx+o+AlYzbohGXYsYsJO474+Ia5VEcpCnMm6YPRhV0H+8bke" +
                    "lgE2vMB2MB54zIxVRVEA1CBPBrWle8qlBMmqXI8ndjQjIYZNZD0CN0ckOgLO3OX8+P6f+zYHbRINCXi" +
                    "T1L7DstJ4FacqE7b2+aNKiMogUoaq7H3dXxJXj32HMFZevrs8ZFxTvbIP4KkazRrdfnZPdWKXk9pv" +
                    "P8EI21RNKKr2NtVNJyRPxI1uWlvYtGeSLcNUioHshNRQ4SxRSG8p1VTBmUpS0cJoZSCmO/0W9doyzwI" +
                    "DAQAB"
                ), 
                "Signature verification failed for DecodeTest."
            );
        }

        [TestMethod]
        public void TestSignature()
        {
            var somePlugin = new PluginConfig
            {
                Name = "Script",
                Description = "A plugin that adds Script as a source",
                Author = "FUTO",
                AuthorUrl = "https://futo.org",
                SourceUrl = "https://futo.org",
                ScriptUrl = "./Script.js",
                Version = 1,
                IconUrl = "./script.png",
                ID = "394dba51-fa0c-450c-8f17-6a00df362218",
                ScriptSignature = "eLdlDIcmpTQmfpCumB5NQwFa0ZDNU8hkRB12/Lg+CdTwPrfTIylGeN6jpTmJrEivyLjj" +
                    "5qHWZeNmrHP++9XFwfwzcaXNspKU9YrL3+Bsy2WNnXfQDeB2t4AkzWYAEfm8/kEcK0Ov8dzy0KW" +
                    "lJsxmW+Oj3mFNVP6PV5ZQY1Gju6W8Jw0sGCxnbuhswtRDPwBKnZQUhlZEXPvbrcblW1q5fCESnf" +
                    "oiJ2MHR5epgHfAuMsoY9EAHVXuyrLvmbWADeVwC5jvWLAkJKw68rQmARqV5BBWkpqFEBQcg50CR" +
                    "vTXtPr8IDjW7yiJ6x9nTG3nokTJn3fj2D3hBEHttEG+KhTMlQ==",
                ScriptPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAucY14D6cl0fK5fHOTUfKM" +
                    "z1iQmfJMgQ+c4MfqlArGCv7YDTvazeQL9dsrCqlYx+o+AlYzbohGXYsYsJO474+Ia5VEcpC" +
                    "nMm6YPRhV0H+8bkelgE2vMB2MB54zIxVRVEA1CBPBrWle8qlBMmqXI8ndjQjIYZNZD0CN0c" +
                    "kOgLO3OX8+P6f+zYHbRINCXiT1L7DstJ4FacqE7b2+aNKiMogUoaq7H3dXxJXj32HMFZevrs8ZF" +
                    "xTvbIP4KkazRrdfnZPdWKXk9pvP8EI21RNKKr2NtVNJyRPxI1uWlvYtGeSLcNUioHshNRQ4SxRSG8p1VTBmUpS0cJoZSCmO/0W9doyzwIDAQAB"
            };
            var script = "//this is just an empty script";

            Assert.IsTrue(somePlugin.VerifySignature(script), "Invalid signature.");
        }

        [TestMethod]
        public void TestSignatureSignSh()
        {
            var somePlugin = new PluginConfig
            {
                Name = "Script",
                Description = "A plugin that adds Script as a source",
                Author = "FUTO",
                AuthorUrl = "https://futo.org",
                SourceUrl = "https://futo.org",
                ScriptUrl = "./Script.js",
                Version = 1,
                IconUrl = "./script.png",
                ID = "394dba51-fa0c-450c-8f17-6a00df362218",
                ScriptSignature = "cQcTqsbGgEXGXycTl+Byf5+DmxTQLE6AjlS+IzRA0Dce915B2brFyr5tymwd2zCEL7HVSpX5g0fia0GRPcujSu21o0yy/iU4AtA1LAP8ZYgQxYSinUs2/q/B3MQYiJCrB9e2K+2Fr8p6CGG5usa4kkYDel4/tofdX3vwQjeEf8hZemPu8Gda39luTqJahQZZ9IZoif8RT9wpQmIXJ4/A+e3PW1m8+3QL3oiMPDos2csFG7Hko/AbBonJJvwr/ywh/+sNutKryvdeIlZgLBMudfrVEfoBdXePhRqSwBpFRuMPmCe63VvhRfu1EzoelGz2cskxsC9ia6b/TE6KPJvJ4w==",
                ScriptPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzhBcOpjK+ArDFL94tXTsnmByyKEjN2LsuXk7Dtl6hKVUxOs7RuZ/Ok+CTacXP3/AmsjfOT9g4Rd5gUg3MHsA0PG5Jc8pzV0FHsTNSO6x9XdJv1CwmMqneQuoNxZfiw6MBCe6y0nMM1vaLYdkXniD39EFHyvYPgSTsF9AUljfmHHH7H7J7rxTbW2p7VYwUp2KNn9Q49G9H7A14hs3y1SsLK2y8fJCQ2XR1ARWrI/6xeOoAo23rSqMrML4O3ukaEG1wjidlPr3zdb8GfSHRBi/0qzTwXx7cFnffPqmoHhnMwmTFlhOO8TdwVRm0cWee3Zz2VsETW4Pd27K8NPH2RELGQIDAQAB"
            };
            var script = "//this is just an empty script";

            Assert.IsTrue(somePlugin.VerifySignature(script), "Invalid signature.");
        }
    }
}
