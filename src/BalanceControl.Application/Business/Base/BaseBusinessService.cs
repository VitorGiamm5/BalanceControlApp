using BalanceControl.Domain.Services.Base;

namespace BalanceControl.Application.Business.Base;

/// <summary>
/// Classe base que implementa o pipeline:
/// PreProcessAsync → ProcessAsync → PostProcessAsync
///
/// Inbox  = TInObject  (dados que entram)
/// Intermediate = TInIntermediate / TOutIntermediate (dados no meio do processo)
/// Outbox = TOutObject (dados que saem)
/// </summary>
public abstract class BaseBusinessService<
        TInObject,
        TInIntermediate,
        TOutIntermediate,
        TOutObject>
    : IBaseBusinessService<TInObject, TOutObject>
    where TInObject : class
    where TInIntermediate : class
    where TOutIntermediate : class
    where TOutObject : class
{
    // ------------------------------------------------------------------ //
    //  Entry-point público — orquestra o pipeline completo
    // ------------------------------------------------------------------ //
    public async Task<TOutObject> ExecuteAsync(TInObject input, CancellationToken ct = default)
    {
        // 1 Inbox — pré-processamento (validação, enriquecimento, sanitização, etc.)
        var processedInput = await PreProcessAsync(input, ct);

        // 2️ PROCESSO — opera no intermediário
        var outIntermediate = await ProcessAsync(processedInput, ct);

        // 3️ Outbox — pós-processamento (mapeamento, auditoria, notificações, etc)
        var result = await PostProcessAsync(outIntermediate, ct);

        return result;
    }

    // ------------------------------------------------------------------ //
    //  Etapas do pipeline — obrigatória apenas ProcessAsync
    // ------------------------------------------------------------------ //

    /// <summary>
    /// INBOX — recebe o input puro e devolve o input pronto para processar.
    /// Use para: validação, enriquecimento, normalização de dados.
    /// Implementação padrão: passa o input sem alteração.
    /// Uso Obrigatório, mas recomendado para manter o ProcessAsync focado apenas na lógica de negócio.
    /// </summary>
    protected abstract Task<TInIntermediate> PreProcessAsync(TInObject input, CancellationToken ct);

    /// <summary>
    /// Conversão de TInObject → TInIntermediate. Ex: Command → Entity ou Query → Entity. ou Objeto intermediário específico para o processo.
    /// Uso opcional, serviços simples podem não precisar de um tipo intermediário e usar TInObject diretamente no ProcessAsync.
    /// </summary>
    protected virtual TInIntermediate? MapToIntermediate(TInObject input)
    {
        // Implementação padrão: tenta converter por cast (útil quando TInObject == TInIntermediate).
        // Retorna null se não for possível — subclasses podem sobrescrever para fornecer mapeamento adequado.
        return input as TInIntermediate;
    }

    /// <summary>
    /// PROCESSAMENTO — lógica de negócio principal opera em TInIntermediate.
    /// Uso Obrigatório.
    /// </summary>
    protected abstract Task<TOutIntermediate> ProcessAsync(TInIntermediate input, CancellationToken ct);

    /// <summary>
    /// OUTBOX — recebe o resultado e o mapeia TOutIntermediate → TOutObject antes de entregar ao caller.
    /// Use para: mapeamento de resposta, auditoria, eventos de domínio, notificações.
    /// Uso Obrigatório, para manter o ProcessAsync focado apenas na lógica de negócio.
    /// </summary>
    protected abstract Task<TOutObject> PostProcessAsync(TOutIntermediate result, CancellationToken ct);

}
