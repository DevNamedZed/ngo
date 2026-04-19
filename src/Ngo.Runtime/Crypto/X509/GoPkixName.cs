using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.X509
{
    [GoType("struct", Name = "Name", Package = "crypto/x509/pkix")]
    public class GoPkixName
    {
        [GoField(Name = "Country")] public Slice<string> Country;
        [GoField(Name = "Organization")] public Slice<string> Organization;
        [GoField(Name = "OrganizationalUnit")] public Slice<string> OrganizationalUnit;
        [GoField(Name = "Locality")] public Slice<string> Locality;
        [GoField(Name = "Province")] public Slice<string> Province;
        [GoField(Name = "StreetAddress")] public Slice<string> StreetAddress;
        [GoField(Name = "PostalCode")] public Slice<string> PostalCode;
        [GoField(Name = "SerialNumber")] public string SerialNumber = "";
        [GoField(Name = "CommonName")] public string CommonName = "";
        [GoField(Name = "Names")] public Slice<GoPkixAttributeTypeAndValue> Names;
        [GoField(Name = "ExtraNames")] public Slice<GoPkixAttributeTypeAndValue> ExtraNames;

        [GoMethod]
        public string String()
        {
            return CommonName;
        }
    }

    [GoType("struct", Name = "AttributeTypeAndValue", Package = "crypto/x509/pkix")]
    public class GoPkixAttributeTypeAndValue
    {
        [GoField(Name = "Type")] public Slice<long> Type;
        [GoField(Name = "Value")] public object? Value;
    }

    [GoType("struct", Name = "Extension", Package = "crypto/x509/pkix")]
    public class GoPkixExtension
    {
        [GoField(Name = "Id")] public Slice<long> Id;
        [GoField(Name = "Critical")] public bool Critical;
        [GoField(Name = "Value", Type = "[]byte")] public Slice<byte> ExtensionValue;
    }

    [GoType("struct", Name = "AlgorithmIdentifier", Package = "crypto/x509/pkix")]
    public class GoPkixAlgorithmIdentifier
    {
        [GoField(Name = "Algorithm")] public Slice<long> Algorithm;
        [GoField(Name = "Parameters")] public object? Parameters;
    }

    [GoType("struct", Name = "RevokedCertificate", Package = "crypto/x509/pkix")]
    public class GoPkixRevokedCertificate
    {
        [GoField(Name = "SerialNumber")] public object? SerialNumber;
        [GoField(Name = "RevocationTime")] public object? RevocationTime;
        [GoField(Name = "Extensions")] public Slice<GoPkixExtension> Extensions;
    }

    [GoType("struct", Name = "CertificateList", Package = "crypto/x509/pkix")]
    public class GoPkixCertificateList
    {
        [GoField(Name = "TBSCertList")] public object? TBSCertList;
        [GoField(Name = "SignatureAlgorithm")] public GoPkixAlgorithmIdentifier SignatureAlgorithm = new();
        [GoField(Name = "SignatureValue")] public object? SignatureValue;

        [GoMethod]
        public bool HasExpired(object? now)
        {
            return false;
        }
    }
}
