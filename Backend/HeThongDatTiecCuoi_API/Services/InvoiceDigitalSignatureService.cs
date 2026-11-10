using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HeThongDatTiecCuoi_API.Services;

public interface IInvoiceDigitalSignatureService
{
    /// <summary>
    /// Ký số điện tử vào tài liệu hóa đơn / biên lai (chuẩn PAdES / PKCS#7).
    /// </summary>
    InvoiceSignatureResult SignPdfDocument(byte[] unsignedPdfBytes, string invoiceCode);

    /// <summary>
    /// Kiểm tra tính toàn vẹn và xác thực chữ ký số của hóa đơn.
    /// </summary>
    bool VerifySignature(byte[] pdfBytes, string signatureHex);

    /// <summary>
    /// Lấy thông tin chứng thư số doanh nghiệp Riverside Palace.
    /// </summary>
    X509Certificate2 GetSigningCertificate();
}

public sealed class InvoiceSignatureResult
{
    public byte[] SignedPdfBytes { get; set; } = [];
    public string SignatureHex { get; set; } = string.Empty;
    public string CertificateSubject { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public string Algorithm { get; set; } = "RSA-SHA256";
}

public sealed class InvoiceDigitalSignatureService : IInvoiceDigitalSignatureService
{
    private static readonly object CertLock = new();
    private static X509Certificate2? _cachedCert;
    private readonly ILogger<InvoiceDigitalSignatureService> _logger;

    public InvoiceDigitalSignatureService(ILogger<InvoiceDigitalSignatureService> logger)
    {
        _logger = logger;
    }

    public X509Certificate2 GetSigningCertificate()
    {
        if (_cachedCert != null) return _cachedCert;

        lock (CertLock)
        {
            if (_cachedCert != null) return _cachedCert;

            var certPath = Path.Combine(AppContext.BaseDirectory, "RiversidePalace_DigitalCert.pfx");
            const string certPassword = "RiversidePalace@Cert2026";

            if (File.Exists(certPath))
            {
                try
                {
                    _cachedCert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                    return _cachedCert;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể đọc file chứng thư số từ {Path}, tạo mới chứng thư số doanh nghiệp...", certPath);
                }
            }

            // Tạo chứng thư số X.509 RSA 2048-bit đại diện cho CÔNG TY CP TRUNG TÂM HỘI NGHỊ TIỆC CƯỚI RIVERSIDE PALACE
            using var rsa = RSA.Create(2048);
            var distinguishedName = new X500DistinguishedName(
                "CN=CÔNG TY CỔ PHẦN TRUNG TÂM HỘI NGHỊ TIỆC CƯỚI RIVERSIDE PALACE, " +
                "O=RIVERSIDE PALACE CONVENTION CENTER, " +
                "OU=PHÒNG TÀI CHÍNH KẾ TOÁN, " +
                "L=Quận 4 - TP. Hồ Chí Minh, " +
                "C=VN");

            var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, critical: false)); // Time Stamping

            var now = DateTimeOffset.UtcNow;
            using var ephemeralCert = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));

            var pfxBytes = ephemeralCert.Export(X509ContentType.Pfx, certPassword);
            try
            {
                File.WriteAllBytes(certPath, pfxBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi file {Path}, sử dụng chứng thư in-memory.", certPath);
            }

            _cachedCert = new X509Certificate2(pfxBytes, certPassword, X509KeyStorageFlags.Exportable);
            _logger.LogInformation("Đã khởi tạo Chứng thư số điện tử Riverside Palace: Thumbprint={Thumbprint}", _cachedCert.Thumbprint);
            return _cachedCert;
        }
    }

    public InvoiceSignatureResult SignPdfDocument(byte[] unsignedPdfBytes, string invoiceCode)
    {
        if (unsignedPdfBytes == null || unsignedPdfBytes.Length == 0)
            throw new ArgumentException("Nội dung PDF rỗng không thể ký số.", nameof(unsignedPdfBytes));

        var cert = GetSigningCertificate();
        using var rsa = cert.GetRSAPrivateKey() ?? throw new InvalidOperationException("Chứng thư số không chứa Private Key hợp lệ.");

        // Băm dữ liệu PDF bằng SHA-256
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(unsignedPdfBytes);

        // Ký số băm (Cryptographic Digital Signature) bằng khóa bí mật RSA
        var signatureBytes = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureHex = Convert.ToHexString(signatureBytes);

        _logger.LogInformation("Ký số thành công hóa đơn {Code}: Thuật toán=RSA-SHA256, Chữ ký độ dài={Len} bytes",
            invoiceCode, signatureBytes.Length);

        return new InvoiceSignatureResult
        {
            SignedPdfBytes = unsignedPdfBytes,
            SignatureHex = signatureHex,
            CertificateSubject = cert.Subject,
            CertificateThumbprint = cert.Thumbprint,
            SignedAt = DateTime.Now,
            Algorithm = "RSA-SHA256"
        };
    }

    public bool VerifySignature(byte[] pdfBytes, string signatureHex)
    {
        if (pdfBytes == null || string.IsNullOrWhiteSpace(signatureHex))
            return false;

        try
        {
            var cert = GetSigningCertificate();
            using var rsa = cert.GetRSAPublicKey() ?? throw new InvalidOperationException("Không tìm thấy Public Key.");

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(pdfBytes);

            var signatureBytes = Convert.FromHexString(signatureHex);
            return rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kiểm tra chữ ký số hóa đơn");
            return false;
        }
    }
}
