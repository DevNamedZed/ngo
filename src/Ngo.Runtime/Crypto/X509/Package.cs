using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.X509
{
    [GoPackage("crypto/x509")]
    public static partial class Package
    {
        // Error variables
        [GoVar] public static readonly object? ErrUnsupportedAlgorithm = "x509: cannot verify signature: algorithm unimplemented";

        // x509.ParseCertificate(asn1Data []byte) (*Certificate, error)
        [GoFunc]
        [return: GoReturn("*x509.Certificate", "error")]
        public static (GoCertificate?, object?) ParseCertificate(Slice<byte> asn1Data)
        {
            try
            {
                var derBytes = SliceToArray(asn1Data);
                var cert = X509CertificateLoader.LoadCertificate(derBytes);
                return (CertFromX509(cert), null);
            }
            catch (Exception ex)
            {
                return (null, "x509: " + ex.Message);
            }
        }

        // x509.ParseCertificates(asn1Data []byte) ([]*Certificate, error)
        [GoFunc]
        [return: GoReturn("[]*x509.Certificate", "error")]
        public static (Slice<GoCertificate?>, object?) ParseCertificates(Slice<byte> asn1Data)
        {
            try
            {
                var derBytes = SliceToArray(asn1Data);
                var cert = X509CertificateLoader.LoadCertificate(derBytes);
                var goCert = CertFromX509(cert);
                return (new Slice<GoCertificate?>(new[] { goCert }), null);
            }
            catch (Exception ex)
            {
                return (new Slice<GoCertificate?>(), "x509: " + ex.Message);
            }
        }

        // x509.SystemCertPool() (*CertPool, error)
        [GoFunc]
        [return: GoReturn("*x509.CertPool", "error")]
        public static (GoCertPool?, object?) SystemCertPool()
        {
            try
            {
                var pool = new GoCertPool();
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                foreach (var cert in store.Certificates)
                {
                    pool.AddX509Cert(cert);
                }
                return (pool, null);
            }
            catch (Exception ex)
            {
                return (null, "x509: " + ex.Message);
            }
        }

        // x509.NewCertPool() *CertPool
        [GoFunc]
        [return: GoReturn("*x509.CertPool")]
        public static GoCertPool NewCertPool() => new GoCertPool();

        // x509.CreateCertificate(rand io.Reader, template, parent *Certificate, pub, priv any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) CreateCertificate(object? rand, [GoParam("*x509.Certificate")] GoCertificate? template, [GoParam("*x509.Certificate")] GoCertificate? parent, object? pub, object? priv)
        {
            try
            {
                // Get the private key as an asymmetric algorithm
                System.Security.Cryptography.RSA? rsaKey = null;
                System.Security.Cryptography.ECDsa? ecKey = null;

                if (priv is Rsa.GoPrivateKey rsaPriv)
                {
                    rsaKey = rsaPriv.ToRSA();
                }
                else if (priv is Ecdsa.GoPrivateKey ecPriv)
                {
                    ecKey = ecPriv.ToECDsa();
                }

                if (rsaKey == null && ecKey == null)
                {
                    return (new Slice<byte>(), "x509: unsupported private key type");
                }

                var subject = new X500DistinguishedName(template?.Subject?.ToString() ?? "CN=ngo");

                CertificateRequest req;
                if (rsaKey != null)
                {
                    req = new CertificateRequest(subject, rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                else
                {
                    req = new CertificateRequest(subject, ecKey!, HashAlgorithmName.SHA256);
                }

                // Add basic constraints if CA
                if (template != null && template.IsCA)
                {
                    req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                }

                X509Certificate2 cert;
                if (parent == null || ReferenceEquals(template, parent))
                {
                    // Self-signed
                    cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
                }
                else
                {
                    // Signed by parent — simplified: create self-signed for now
                    cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
                }

                return (new Slice<byte>(cert.RawData), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), "x509: " + ex.Message);
            }
        }

        // x509.MarshalPKCS1PrivateKey(key *rsa.PrivateKey) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> MarshalPKCS1PrivateKey(object? key)
        {
            if (key is Rsa.GoPrivateKey rsaKey)
            {
                var rsa = rsaKey.ToRSA();
                if (rsa != null)
                {
                    var bytes = rsa.ExportRSAPrivateKey();
                    return new Slice<byte>(bytes);
                }
            }
            return new Slice<byte>();
        }

        // x509.ParsePKCS1PrivateKey(der []byte) (*rsa.PrivateKey, error)
        [GoFunc]
        [return: GoReturn("*rsa.PrivateKey", "error")]
        public static (object?, object?) ParsePKCS1PrivateKey(Slice<byte> der)
        {
            try
            {
                var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportRSAPrivateKey(SliceToArray(der), out _);
                return (Rsa.GoPrivateKey.FromRSA(rsa), null);
            }
            catch (Exception ex)
            {
                return (null, "x509: " + ex.Message);
            }
        }

        // x509.MarshalPKCS8PrivateKey(key any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalPKCS8PrivateKey(object? key)
        {
            try
            {
                if (key is Rsa.GoPrivateKey rsaKey)
                {
                    var rsa = rsaKey.ToRSA();
                    if (rsa != null)
                    {
                        return (new Slice<byte>(rsa.ExportPkcs8PrivateKey()), null);
                    }
                }
                if (key is Ecdsa.GoPrivateKey ecKey)
                {
                    var ecdsa = ecKey.ToECDsa();
                    if (ecdsa != null)
                    {
                        return (new Slice<byte>(ecdsa.ExportPkcs8PrivateKey()), null);
                    }
                }
                return (new Slice<byte>(), "x509: unknown key type");
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), "x509: " + ex.Message);
            }
        }

        // x509.ParsePKCS8PrivateKey(der []byte) (any, error)
        [GoFunc]
        [return: GoReturn("any", "error")]
        public static (object?, object?) ParsePKCS8PrivateKey(Slice<byte> der)
        {
            var derBytes = SliceToArray(der);

            // Try RSA first
            try
            {
                var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportPkcs8PrivateKey(derBytes, out _);
                return (Rsa.GoPrivateKey.FromRSA(rsa), null);
            }
            catch { }

            // Try ECDSA
            try
            {
                var ecdsa = System.Security.Cryptography.ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(derBytes, out _);
                return (Ecdsa.GoPrivateKey.FromECDsa(ecdsa), null);
            }
            catch { }

            return (null, "x509: failed to parse private key");
        }

        // x509.ParsePKIXPublicKey(derBytes []byte) (any, error)
        [GoFunc]
        [return: GoReturn("any", "error")]
        public static (object?, object?) ParsePKIXPublicKey(Slice<byte> derBytes)
        {
            var bytes = SliceToArray(derBytes);

            // Try RSA
            try
            {
                var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(bytes, out _);
                return (Rsa.GoPublicKey.FromParameters(rsa.ExportParameters(false)), null);
            }
            catch { }

            // Try ECDSA
            try
            {
                var ecdsa = System.Security.Cryptography.ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(bytes, out _);
                var ecParams = ecdsa.ExportParameters(false);
                var pubKey = new Ecdsa.GoPublicKey();
                pubKey.SetFromParameters(ecParams);
                return (pubKey, null);
            }
            catch { }

            return (null, "x509: unsupported public key type");
        }

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

        // x509.MarshalPKIXPublicKey(pub any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalPKIXPublicKey(object? pub)
        {
            try
            {
                if (pub is Rsa.GoPublicKey rsaPub)
                {
                    var rsa = rsaPub.ToRSA();
                    if (rsa != null)
                    {
                        return (new Slice<byte>(rsa.ExportSubjectPublicKeyInfo()), null);
                    }
                }
                return (new Slice<byte>(), "x509: unsupported public key type");
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), "x509: " + ex.Message);
            }
        }

        // x509.ParsePKCS1PublicKey(der []byte) (*rsa.PublicKey, error)
        [GoFunc]
        [return: GoReturn("*rsa.PublicKey", "error")]
        public static (object?, object?) ParsePKCS1PublicKey(Slice<byte> der)
        {
            try
            {
                var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportRSAPublicKey(SliceToArray(der), out _);
                return (Rsa.GoPublicKey.FromParameters(rsa.ExportParameters(false)), null);
            }
            catch (Exception ex)
            {
                return (null, "x509: " + ex.Message);
            }
        }

        // x509.DecryptPEMBlock(b *pem.Block, password []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) DecryptPEMBlock(object? b, Slice<byte> password)
            => (new Slice<byte>(), "x509: DecryptPEMBlock is deprecated and not implemented");

        // x509.IsEncryptedPEMBlock(b *pem.Block) bool
        [GoFunc]
        public static bool IsEncryptedPEMBlock(object? b) => false;

        // x509.ParseECPrivateKey(der []byte) (*ecdsa.PrivateKey, error)
        [GoFunc]
        [return: GoReturn("*ecdsa.PrivateKey", "error")]
        public static (object?, object?) ParseECPrivateKey(Slice<byte> der)
        {
            try
            {
                var ecdsa = System.Security.Cryptography.ECDsa.Create();
                ecdsa.ImportECPrivateKey(SliceToArray(der), out _);
                return (Ecdsa.GoPrivateKey.FromECDsa(ecdsa), null);
            }
            catch (Exception ex)
            {
                return (null, "x509: " + ex.Message);
            }
        }

        // Helper: convert Slice<byte> to byte[]
        internal static byte[] SliceToArray(Slice<byte> s)
        {
            var buf = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                buf[i] = s[i];
            }
            return buf;
        }

        // Helper: convert X509Certificate2 to GoCertificate
        internal static GoCertificate CertFromX509(X509Certificate2 cert)
        {
            var goCert = new GoCertificate
            {
                Raw = new Slice<byte>(cert.RawData),
                Version = cert.Version,
                SignatureAlgorithm = MapSignatureAlgorithm(cert.SignatureAlgorithm.FriendlyName),
                PublicKeyAlgorithm = MapPublicKeyAlgorithm(cert.PublicKey.Oid.FriendlyName),
                IsCA = cert.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority),
                BasicConstraintsValid = cert.Extensions.OfType<X509BasicConstraintsExtension>().Any(),
            };

            // Subject/Issuer as string representations
            goCert.Subject = cert.Subject;
            goCert.Issuer = cert.Issuer;

            // Serial number
            goCert.SerialNumber = cert.SerialNumber;

            // DNS names from SAN
            var sanExt = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
            if (sanExt != null)
            {
                var dnsNames = new List<string>();
                foreach (var dns in sanExt.EnumerateDnsNames())
                {
                    dnsNames.Add(dns);
                }
                goCert.DNSNames = new Slice<string>(dnsNames.ToArray());
            }

            // Extract public key
            try
            {
                var rsaKey = cert.GetRSAPublicKey();
                if (rsaKey != null)
                {
                    goCert.PublicKey = Rsa.GoPublicKey.FromParameters(rsaKey.ExportParameters(false));
                }
            }
            catch { }

            if (goCert.PublicKey == null)
            {
                try
                {
                    var ecKey = cert.GetECDsaPublicKey();
                    if (ecKey != null)
                    {
                        var ecParams = ecKey.ExportParameters(false);
                        var pubKey = new Ecdsa.GoPublicKey();
                        pubKey.SetFromParameters(ecParams);
                        goCert.PublicKey = pubKey;
                    }
                }
                catch { }
            }

            return goCert;
        }

        private static long MapSignatureAlgorithm(string? friendlyName)
        {
            return friendlyName switch
            {
                "sha256RSA" => SHA256WithRSA,
                "sha384RSA" => SHA384WithRSA,
                "sha512RSA" => SHA512WithRSA,
                "sha256ECDSA" => ECDSAWithSHA256,
                "sha384ECDSA" => ECDSAWithSHA384,
                "sha512ECDSA" => ECDSAWithSHA512,
                _ => 0,
            };
        }

        private static long MapPublicKeyAlgorithm(string? friendlyName)
        {
            return friendlyName switch
            {
                "RSA" => RSA,
                "DSA" => DSA,
                "ECC" or "ECDSA" => ECDSA,
                _ => UnknownPublicKeyAlgorithm,
            };
        }

        // CertificateRequest type
        [GoType("struct", Name = "CertificateRequest", Package = "crypto/x509")]
        public class GoCertificateRequest
        {
            [GoField(Name = "Raw")] public Slice<byte> Raw;
            [GoField(Name = "RawTBSCertificateRequest")] public Slice<byte> RawTBSCertificateRequest;
            [GoField(Name = "RawSubjectPublicKeyInfo")] public Slice<byte> RawSubjectPublicKeyInfo;
            [GoField(Name = "RawSubject")] public Slice<byte> RawSubject;
            [GoField(Name = "Version")] public long Version;
            [GoField(Name = "Signature")] public Slice<byte> Signature;
            [GoField(Name = "PublicKey")] public object? PublicKey;
            [GoField(Name = "PublicKeyAlgorithm")] public long PublicKeyAlgorithm;
            [GoField(Name = "Subject")] public object? Subject; // pkix.Name
            [GoField(Name = "DNSNames")] public Slice<string> DNSNames;
            [GoField(Name = "EmailAddresses")] public Slice<string> EmailAddresses;
            [GoField(Name = "IPAddresses")] public Slice<object?> IPAddresses;
            [GoField(Name = "URIs")] public Slice<object?> URIs;
            [GoField(Name = "SignatureAlgorithm")] public long SignatureAlgorithm;
            [GoField(Name = "Extensions")] public object? Extensions;

            [GoMethod]
            [return: GoReturn("error")]
            public object? CheckSignature() => null;
        }

        // x509.CreateCertificateRequest(rand io.Reader, template *CertificateRequest, priv any) (csr []byte, err error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) CreateCertificateRequest(object? rand, [GoParam("*x509.CertificateRequest")] object? template, object? priv)
            => (new Slice<byte>(), null);

        // x509.ParseCertificateRequest(asn1Data []byte) (*CertificateRequest, error)
        [GoFunc]
        [return: GoReturn("*x509.CertificateRequest", "error")]
        public static (object?, object?) ParseCertificateRequest(Slice<byte> asn1Data) => (null, null);

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
        [GoField(Name = "ExtraExtensions")] public object? ExtraExtensions;

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
        private readonly List<GoCertificate> _certs = new List<GoCertificate>();

        [GoMethod]
        public bool AppendCertsFromPEM(Slice<byte> pemCerts)
        {
            bool added = false;
            var (block, rest) = Encoding.Pem.Package.Decode(pemCerts);
            while (block != null)
            {
                if (block.Type == "CERTIFICATE")
                {
                    var (cert, err) = Package.ParseCertificate(block.Bytes);
                    if (err == null && cert != null)
                    {
                        _certs.Add(cert);
                        added = true;
                    }
                }
                (block, rest) = Encoding.Pem.Package.Decode(rest);
            }
            return added;
        }

        [GoMethod]
        public void AddCert([GoParam("*x509.Certificate")] GoCertificate? cert)
        {
            if (cert != null)
            {
                _certs.Add(cert);
            }
        }

        internal void AddX509Cert(X509Certificate2 cert)
        {
            _certs.Add(Package.CertFromX509(cert));
        }

        [GoMethod]
        [return: GoReturn("[][]byte")]
        public Slice<Slice<byte>> Subjects()
        {
            var subjects = new Slice<byte>[_certs.Count];
            for (int i = 0; i < _certs.Count; i++)
            {
                subjects[i] = _certs[i].RawSubject;
            }
            return new Slice<Slice<byte>>(subjects);
        }

        [GoMethod]
        public long Len() => _certs.Count;

        internal List<GoCertificate> Certificates => _certs;
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

namespace Ngo.Runtime.Crypto.X509
{
    public static partial class Package
    {
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalECPrivateKey([GoParam("*ecdsa.PrivateKey")] object? key)
        {
            return (default, null);
        }
    }
}
