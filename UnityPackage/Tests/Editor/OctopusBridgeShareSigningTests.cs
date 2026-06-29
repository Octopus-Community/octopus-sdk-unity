using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class OctopusBridgeShareSigningTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void SignatureRequest_WithSigner_RelaysFingerprintAndRecordsJwt()
    {
        string seenFingerprint = null;
        var post = new OctopusPrefilledPost
        {
            Text = "hi",
            ImagePath = "/tmp/a.png",
            SignBridgeShare = fp => { seenFingerprint = fp; return Task.FromResult("jwt.abc"); }
        };
        OctopusSDK.OpenCreatePost(post);

        OctopusSDK.Mock.EmitBridgeShareSignatureRequest("fp123");

        Assert.AreEqual("fp123", seenFingerprint);
        Assert.AreEqual("jwt.abc", OctopusSDK.Mock.LastBridgeShareSignature);
        Assert.IsFalse(OctopusSDK.Mock.LastBridgeShareSignatureFailed);
    }

    [Test]
    public void SignatureRequest_NoSigner_Fails()
    {
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = "hi" }); // no SignBridgeShare
        OctopusSDK.Mock.EmitBridgeShareSignatureRequest("fp123");
        Assert.IsTrue(OctopusSDK.Mock.LastBridgeShareSignatureFailed);
        Assert.IsNull(OctopusSDK.Mock.LastBridgeShareSignature);
    }

    [Test]
    public void OpenWithoutSigner_ClearsPreviousSigner()
    {
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost
        {
            Text = "first", SignBridgeShare = fp => Task.FromResult("stale.jwt")
        });
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = "second" }); // no signer
        OctopusSDK.Mock.EmitBridgeShareSignatureRequest("fp123");
        Assert.IsTrue(OctopusSDK.Mock.LastBridgeShareSignatureFailed); // stale signer not reused
    }

    [Test]
    public void SignatureRequest_SignerThrows_Fails()
    {
        LogAssert.Expect(LogType.Exception, "Exception: backend down");
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost
        {
            Text = "hi",
            ImagePath = "/tmp/a.png",
            SignBridgeShare = fp => throw new System.Exception("backend down")
        });
        OctopusSDK.Mock.EmitBridgeShareSignatureRequest("fp123");
        Assert.IsTrue(OctopusSDK.Mock.LastBridgeShareSignatureFailed);
    }

    [Test]
    public void SignatureRequest_SignerReturnsEmpty_Fails()
    {
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost
        {
            Text = "hi",
            ImagePath = "/tmp/a.png",
            SignBridgeShare = fp => Task.FromResult("") // host declines / empty -> treated as a failure
        });
        OctopusSDK.Mock.EmitBridgeShareSignatureRequest("fp123");
        Assert.IsTrue(OctopusSDK.Mock.LastBridgeShareSignatureFailed);
    }
}
