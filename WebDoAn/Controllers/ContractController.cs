using Microsoft.AspNetCore.Mvc;
using WebDoAn.Data;
using WebDoAn.Models;
using WebDoAn.Services;

namespace WebDoAn.Controllers;

public class ContractController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;

    public ContractController(ApplicationDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> SendContractToTenant(int roomId, string tenantEmail, string contractContent)
    {
        var landlordEmail = HttpContext.Session.GetString("CURRENT_USER_EMAIL") ?? User.Identity?.Name;
        if (string.IsNullOrEmpty(landlordEmail)) return RedirectToAction("Login", "Account");

        var contract = new RoomContract
        {
            RoomPostId = roomId,
            LandlordEmail = landlordEmail,
            TenantEmail = tenantEmail,
            ContractContent = contractContent,
            Status = "Sent",
            CreatedAt = DateTime.Now
        };

        _context.RoomContracts.Add(contract);
        await _context.SaveChangesAsync();

        var callbackUrl = Url.Action("Start_eKYC", "Contract", new { contractId = contract.Id }, protocol: Request.Scheme);
        await _emailService.SendEmailAsync(tenantEmail, "Yêu cầu ký hợp đồng", $"<a href='{callbackUrl}'>BẤM VÀO ĐÂY ĐỂ KÝ HỢP ĐỒNG</a>");

        return RedirectToAction("Manage", "Room", new { id = roomId });
    }

    [HttpGet]
    public IActionResult Start_eKYC(int contractId)
    {
        var email = HttpContext.Session.GetString("CURRENT_USER_EMAIL") ?? User.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return Redirect($"/Identity/Account/Login?ReturnUrl=/Contract/Start_eKYC?contractId={contractId}");

        var contract = _context.RoomContracts.Find(contractId);
        if (contract == null || contract.TenantEmail.ToLower() != email.ToLower()) return RedirectToAction("Index", "Home");

        ViewBag.ContractId = contractId;
        return View("eKYC");
    }

    [HttpPost]
    public IActionResult Save_eKYC(int contractId, string faceImageData)
    {
        var contract = _context.RoomContracts.Find(contractId);
        if (contract == null) return NotFound();
        contract.Status = "eKYC_Done";
        contract.TenantFaceImageUrl = faceImageData;
        _context.SaveChanges();
        return RedirectToAction("SignAndOTP", new { contractId = contract.Id });
    }

    [HttpGet]
    public IActionResult SignAndOTP(int contractId)
    {
        var contract = _context.RoomContracts.Find(contractId);
        return View(contract);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateAndSendOTP(int contractId)
    {
        var contract = _context.RoomContracts.Find(contractId);
        string otp = new Random().Next(100000, 999999).ToString();
        contract.OtpCode = otp;
        contract.OtpExpiry = DateTime.Now.AddMinutes(5);
        _context.SaveChanges();
        await _emailService.SendEmailAsync(contract.TenantEmail, "Mã OTP", $"Mã OTP của bạn là: {otp}");
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmSignContract(int contractId, string otpInput, string signatureBase64)
    {
        var contract = _context.RoomContracts.Find(contractId);
        if (contract.OtpCode != otpInput || DateTime.Now > contract.OtpExpiry) return RedirectToAction("SignAndOTP", new { contractId });

        // SỬA LỖI VỠ ẢNH: Phục hồi dấu '+' trong chuỗi Base64
        if (!string.IsNullOrEmpty(signatureBase64)) signatureBase64 = signatureBase64.Replace(" ", "+");

        contract.TenantSignatureUrl = signatureBase64;
        contract.Status = "TenantSigned";
        contract.SignedAt = DateTime.Now;

        string signatureTable = $@"<br><br><table style='width: 100%; text-align: center;'><tr>
            <td style='width: 50%;'><strong>BÊN CHO THUÊ (BÊN A)</strong><br><i>(Chưa ký)</i></td>
            <td style='width: 50%;'><strong>BÊN THUÊ (BÊN B)</strong><br><img src='{signatureBase64}' style='max-height: 100px;' /></td>
            </tr></table>";
        contract.ContractContent += signatureTable;
        _context.SaveChanges();

        var callbackUrl = Url.Action("LandlordSign", "Contract", new { contractId = contract.Id }, protocol: Request.Scheme);
        await _emailService.SendEmailAsync(contract.LandlordEmail, "Người thuê đã ký", $"<a href='{callbackUrl}'>CHỦ TRỌ KÝ XÁC NHẬN</a>");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult LandlordSign(int contractId)
    {
        var email = HttpContext.Session.GetString("CURRENT_USER_EMAIL") ?? User.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return Redirect($"/Identity/Account/Login?ReturnUrl=/Contract/LandlordSign?contractId={contractId}");
        return View(_context.RoomContracts.Find(contractId));
    }

    [HttpPost]
    public async Task<IActionResult> GenerateLandlordOTP(int contractId)
    {
        var contract = _context.RoomContracts.Find(contractId);
        string otp = new Random().Next(100000, 999999).ToString();
        contract.OtpCode = otp; _context.SaveChanges();
        await _emailService.SendEmailAsync(contract.LandlordEmail, "OTP Chủ Trọ", $"OTP: {otp}");
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmLandlordSign(int contractId, string otpInput, string signatureBase64)
    {
        var contract = _context.RoomContracts.Find(contractId);
        if (contract.OtpCode != otpInput) return RedirectToAction("LandlordSign", new { contractId });

        // SỬA LỖI VỠ ẢNH
        if (!string.IsNullOrEmpty(signatureBase64)) signatureBase64 = signatureBase64.Replace(" ", "+");

        contract.Status = "Completed";
        string landlordSig = $"<strong>BÊN CHO THUÊ (BÊN A)</strong><br><img src='{signatureBase64}' style='max-height: 100px;' />";
        contract.ContractContent = contract.ContractContent.Replace("<strong>BÊN CHO THUÊ (BÊN A)</strong><br><i>(Chưa ký)</i>", landlordSig);
        _context.SaveChanges();

        // GỬI EMAIL CHỨA LINK XEM HỢP ĐỒNG (SỬA LỖI GMAIL CHẶN ẢNH)
        var viewUrl = Url.Action("ViewContract", "Contract", new { contractId = contract.Id }, protocol: Request.Scheme);
        string emailBody = $"<h3>Hợp đồng thành công!</h3><p>Vui lòng bấm vào nút để xem bản gốc có chữ ký:</p><a href='{viewUrl}' style='padding:10px; background:#009688; color:white; text-decoration:none;'>XEM HỢP ĐỒNG</a>";

        await _emailService.SendEmailAsync(contract.TenantEmail, "Hợp đồng chính thức", emailBody);
        await _emailService.SendEmailAsync(contract.LandlordEmail, "Hợp đồng chính thức", emailBody);

        return RedirectToAction("MyRooms", "Room");
    }

    [HttpGet]
    public IActionResult ViewContract(int contractId)
    {
        return View(_context.RoomContracts.Find(contractId));
    }
}