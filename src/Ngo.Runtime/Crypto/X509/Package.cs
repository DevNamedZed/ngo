using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.X509
{
    [GoPackage("crypto/x509")]
    public static class Package
    {
        // Error variables
        [GoVar] public static readonly object? ErrUnsupportedAlgorithm = "x509: cannot verify signature: algorithm unimplemented";

        // x509.ParseCertificate(asn1Data []byte) (*Certificate, error)
        [GoFunc]
        [return: GoReturn("*x509.Certificate", "error")]
        public static (GoCertificate?, object?) ParseCertificate(Slice<byte> asn1Data) => (new GoCertificate(), null);

        // x509.ParseCertificates(asn1Data []byte) ([]*Certificate, error)
        [GoFunc]
        [return: GoReturn("[]*x509.Certificate", "error")]
        public static (Slice<GoCertificate?>, object?) ParseCertificates(Slice<byte> asn1Data) => (new Slice<GoCertificate?>(), null);

        // x509.SystemCertPool() (*CertPool, error)
        [GoFunc]
        [return: GoReturn("*x509.CertPool", "error")]
        public static (GoCertPool?, object?) SystemCertPool() => (new GoCertPool(), null);

        // x509.NewCertPool() *CertPool
        [GoFunc]
        [return: GoReturn("*x509.CertPool")]
        public static GoCertPool NewCertPool() => new GoCertPool();

        // x509.CreateCertificate(rand io.Reader, template, parent *Certificate, pub, priv any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) CreateCertificate(object? rand, [GoParam("*x509.Certificate")] GoCertificate? template, [GoParam("*x509.Certificate")] GoCertificate? parent, object? pub, object? priv)
            => (new Slice<byte>(), null);

        // x509.MarshalPKCS1PrivateKey(key *rsa.PrivateKey) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> MarshalPKCS1PrivateKey(object? key) => new Slice<byte>();

        // x509.ParsePKCS1PrivateKey(der []byte) (*rsa.PrivateKey, error)
        [GoFunc]
        [return: GoReturn("*rsa.PrivateKey", "error")]
        public static (object?, object?) ParsePKCS1PrivateKey(Slice<byte> der) => (null, null);

        // x509.MarshalPKCS8PrivateKey(key any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalPKCS8PrivateKey(object? key) => (new Slice<byte>(), null);

        // x509.ParsePKCS8PrivateKey(der []byte) (any, error)
        [GoFunc]
        [return: GoReturn("any", "error")]
        public static (object?, object?) ParsePKCS8PrivateKey(Slice<byte> der) => (null, null);

        // x509.ParsePKIXPublicKey(derBytes []byte) (any, error)
        [GoFunc]
        [return: GoReturn("any", "error")]
        public static (object?, object?) ParsePKIXPublicKey(Slice<byte> derBytes) => (null, null);

        // KeyUsage constants
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageDigitalSignature = 1;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageContentCommitment = 2;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageKeyEncipherment = 4;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageDataEncipherment = 8;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageKeyAgreement = 16;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageCertSign = 32;
        [GoConst(Type = "x509.KeyUsage")]
        public const long KeyUsageCRLSign = 64;

        // ExtKeyUsage constants
        [GoConst(Type = "x509.ExtKeyUsage")]
        public const long ExtKeyUsageAny = 0;
        [GoConst(Type = "x509.ExtKeyUsage")]
        public const long ExtKeyUsageServerAuth = 1;
        [GoConst(Type = "x509.ExtKeyUsage")]
        public const long ExtKeyUsageClientAuth = 2;

        // SignatureAlgorithm constants
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long SHA256WithRSA = 4;
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long SHA384WithRSA = 5;
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long SHA512WithRSA = 6;
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long ECDSAWithSHA256 = 7;
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long ECDSAWithSHA384 = 8;
        [GoConst(Type = "x509.SignatureAlgorithm")]
        public const long ECDSAWithSHA512 = 9;

        // PublicKeyAlgorithm constants
        [GoConst(Type = "x509.PublicKeyAlgorithm")]
        public const long UnknownPublicKeyAlgorithm = 0;
        [GoConst(Type = "x509.PublicKeyAlgorithm")]
        public const long RSA = 1;
        [GoConst(Type = "x509.PublicKeyAlgorithm")]
        public const long DSA = 2;
        [GoConst(Type = "x509.PublicKeyAlgorithm")]
        public const long ECDSA = 3;
        [GoConst(Type = "x509.PublicKeyAlgorithm")]
        public const long Ed25519 = 4;

        // x509.ParseECPrivateKey(der []byte) (*ecdsa.PrivateKey, error)
        [GoFunc]
        [return: GoReturn("*ecdsa.PrivateKey", "error")]
        public static (object?, object?) ParseECPrivateKey(Slice<byte> der) => (null, null);

        // Error variables
        [GoVar] public static readonly object? ErrCertificateInvalid = "x509: certificate is not authorized";

        // InvalidReason constants
        [GoConst(Type = "x509.InvalidReason")]
        public const long NotAuthorizedToSign = 0;
        [GoConst(Type = "x509.InvalidReason")]
        public const long Expired = 1;
        [GoConst(Type = "x509.InvalidReason")]
        public const long CANotAuthorizedForThisName = 2;
        [GoConst(Type = "x509.InvalidReason")]
        public const long TooManyIntermediates = 3;
        [GoConst(Type = "x509.InvalidReason")]
        public const long IncompatibleUsage = 4;
        [GoConst(Type = "x509.InvalidReason")]
        public const long NameMismatch = 5;
        [GoConst(Type = "x509.InvalidReason")]
        public const long NameConstraintsWithoutSANs = 6;
        [GoConst(Type = "x509.InvalidReason")]
        public const long UnconstrainedName = 7;
        [GoConst(Type = "x509.InvalidReason")]
        public const long TooManyConstraints = 8;
        [GoConst(Type = "x509.InvalidReason")]
        public const long CANotAuthorizedForExtKeyUsage = 9;
    }

    // x509.Certificate struct
    [GoType("struct", Name = "Certificate", Package = "crypto/x509")]
    public class GoCertificate
    {
        [GoField(Name = "Raw")] public Slice<byte> Raw;
        [GoField(Name = "RawTBSCertificate")] public Slice<byte> RawTBSCertificate;
        [GoField(Name = "RawSubjectPublicKeyInfo")] public Slice<byte> RawSubjectPublicKeyInfo;
        [GoField(Name = "RawSubject")] public Slice<byte> RawSubject;
        [GoField(Name = "RawIssuer")] public Slice<byte> RawIssuer;
        [GoField(Name = "Signature")] public Slice<byte> Signature;
        [GoField(Name = "PublicKey")] public object? PublicKey; // any
        [GoField(Name = "Version")] public long Version;
        [GoField(Name = "SerialNumber")] public object? SerialNumber; // *big.Int
        [GoField(Name = "Issuer")] public object? Issuer; // pkix.Name
        [GoField(Name = "Subject")] public object? Subject; // pkix.Name
        [GoField(Name = "NotBefore")] public object? NotBefore; // time.Time
        [GoField(Name = "NotAfter")] public object? NotAfter; // time.Time
        [GoField(Name = "KeyUsage")] public long KeyUsage; // KeyUsage
        [GoField(Name = "ExtKeyUsage")] public Slice<long> ExtKeyUsage;
        [GoField(Name = "DNSNames")] public Slice<string> DNSNames;
        [GoField(Name = "EmailAddresses")] public Slice<string> EmailAddresses;
        [GoField(Name = "IPAddresses")] public Slice<object?> IPAddresses;
        [GoField(Name = "IsCA")] public bool IsCA;
        [GoField(Name = "BasicConstraintsValid")] public bool BasicConstraintsValid;
        [GoField(Name = "MaxPathLen")] public long MaxPathLen;
        [GoField(Name = "MaxPathLenZero")] public bool MaxPathLenZero;
        [GoField(Name = "SignatureAlgorithm")] public long SignatureAlgorithm;
        [GoField(Name = "PublicKeyAlgorithm")] public long PublicKeyAlgorithm;
        [GoField(Name = "SubjectKeyId")] public Slice<byte> SubjectKeyId;
        [GoField(Name = "AuthorityKeyId")] public Slice<byte> AuthorityKeyId;
        [GoField(Name = "OCSPServer")] public Slice<string> OCSPServer;
        [GoField(Name = "IssuingCertificateURL")] public Slice<string> IssuingCertificateURL;
        [GoField(Name = "CRLDistributionPoints")] public Slice<string> CRLDistributionPoints;
        [GoField(Name = "PermittedDNSDomains")] public Slice<string> PermittedDNSDomains;
        [GoField(Name = "ExcludedDNSDomains")] public Slice<string> ExcludedDNSDomains;
        [GoField(Name = "PermittedDNSDomainsCritical")] public bool PermittedDNSDomainsCritical;
        [GoField(Name = "URIs")] public Slice<object?> URIs;

        [GoMethod]
        [return: GoReturn("error")]
        public object? CheckSignatureFrom([GoParam("*x509.Certificate")] GoCertificate? parent) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? VerifyHostname(string h) => null;

        [GoMethod]
        public bool Equal([GoParam("*x509.Certificate")] GoCertificate? other) => false;

        [GoMethod]
        [return: GoReturn("[][]*x509.Certificate", "error")]
        public (Slice<Slice<GoCertificate?>>, object?) Verify([GoParam("x509.VerifyOptions")] GoVerifyOptions opts)
            => (new Slice<Slice<GoCertificate?>>(), null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? CheckSignature([GoParam("x509.SignatureAlgorithm")] long algo, Slice<byte> signed, Slice<byte> signature) => null;
    }

    // x509.CertPool struct
    [GoType("struct", Name = "CertPool", Package = "crypto/x509")]
    public class GoCertPool
    {
        [GoMethod]
        public bool AppendCertsFromPEM(Slice<byte> pemCerts) => false;

        [GoMethod]
        public void AddCert([GoParam("*x509.Certificate")] GoCertificate? cert) { }

        [GoMethod]
        [return: GoReturn("[][]byte")]
        public Slice<Slice<byte>> Subjects() => new Slice<Slice<byte>>();

        [GoMethod]
        public long Len() => 0;
    }

    // Named types
    [GoType("named", Name = "KeyUsage", Package = "crypto/x509", Underlying = "int")]
    public struct GoKeyUsage { public long Value; }

    [GoType("named", Name = "ExtKeyUsage", Package = "crypto/x509", Underlying = "int")]
    public struct GoExtKeyUsage { public long Value; }

    [GoType("named", Name = "SignatureAlgorithm", Package = "crypto/x509", Underlying = "int")]
    public struct GoSignatureAlgorithm { public long Value; }

    [GoType("named", Name = "PublicKeyAlgorithm", Package = "crypto/x509", Underlying = "int")]
    public struct GoPublicKeyAlgorithm { public long Value; }

    [GoType("named", Name = "InvalidReason", Package = "crypto/x509", Underlying = "int")]
    public struct GoInvalidReason { public long Value; }

    // x509.VerifyOptions struct
    [GoType("struct", Name = "VerifyOptions", Package = "crypto/x509")]
    public class GoVerifyOptions
    {
        [GoField(Name = "DNSName")] public string DNSName = "";
        [GoField(Name = "Intermediates", Type = "*x509.CertPool")] public GoCertPool? Intermediates;
        [GoField(Name = "Roots", Type = "*x509.CertPool")] public GoCertPool? Roots;
        [GoField(Name = "CurrentTime")] public object? CurrentTime; // time.Time
        [GoField(Name = "KeyUsages")] public Slice<long> KeyUsages;
        [GoField(Name = "MaxConstraintComparisions")] public long MaxConstraintComparisions;
    }

    // x509.CertificateInvalidError struct
    [GoType("struct", Name = "CertificateInvalidError", Package = "crypto/x509")]
    public class GoCertificateInvalidError
    {
        [GoField(Name = "Cert", Type = "*x509.Certificate")] public GoCertificate? Cert;
        [GoField(Name = "Reason")] public long Reason;

        [GoMethod]
        public string Error() => "x509: certificate is not valid";
    }

    // x509.UnknownAuthorityError struct
    [GoType("struct", Name = "UnknownAuthorityError", Package = "crypto/x509")]
    public class GoUnknownAuthorityError
    {
        [GoField(Name = "Cert", Type = "*x509.Certificate")] public GoCertificate? Cert;

        [GoMethod]
        public string Error() => "x509: certificate signed by unknown authority";
    }
}
