namespace HelpDeskPro.Models;

/// <summary>Dados apresentados pela página de erro padrão do ASP.NET Core.</summary>
public class ErrorViewModel
{
    // ID da requisição para ajudar a localizar o erro nos registos do servidor.
    public string? RequestId { get; set; }

    // A vista só mostra o ID quando este foi fornecido pelo middleware.
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
