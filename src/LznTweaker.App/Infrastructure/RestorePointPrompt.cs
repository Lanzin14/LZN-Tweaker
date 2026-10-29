using LznTweaker.Core.Changes;

namespace LznTweaker.Infrastructure;

/// <summary>
/// Fluxo comum antes de qualquer lote: bloqueia quando o ponto de restauração obrigatório não pode ser criado e,
/// se a Proteção do Sistema estiver desativada, oferece ativá-la com consentimento.
/// </summary>
public sealed class RestorePointPrompt(RestorePointGuard guard, IUserDialogs dialogs)
{
    public async Task<bool> EnsureAvailableAsync()
    {
        var readiness = await guard.CheckAsync();
        if (readiness.CanAttempt)
            return true;

        if (!readiness.CanEnableProtection)
        {
            dialogs.ShowError(readiness.Message + "\n\nO lote não será executado.", "Ponto de restauração indisponível");
            return false;
        }

        if (!dialogs.Confirm(
                readiness.Message + "\n\n" +
                "O LznTweaker só aplica alterações depois de criar um ponto de restauração.\n\n" +
                "Deseja ativar a Proteção do Sistema no disco do Windows agora?\n" +
                "• O Windows reserva parte do disco para os pontos (ajustável em Proteção do Sistema).\n" +
                "• A ativação será registrada no histórico.",
                "Ativar Proteção do Sistema"))
            return false;

        var result = await guard.EnableProtectionAsync();
        if (result.Outcome is "Sucesso verificado" or "Nada a alterar")
            return true;

        dialogs.ShowError($"A Proteção do Sistema não foi ativada: {result.Message}\n\nO lote não será executado.", "Ativar Proteção do Sistema");
        return false;
    }
}
