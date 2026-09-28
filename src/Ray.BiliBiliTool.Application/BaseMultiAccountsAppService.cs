using Microsoft.Extensions.Logging;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Application.Contracts;
using Ray.BiliBiliTool.DomainService.Interfaces;
using Ray.BiliBiliTool.Infrastructure.Cookie;

namespace Ray.BiliBiliTool.Application;

public abstract class BaseMultiAccountsAppService(
    ILogger logger,
    CookieStrFactory<BiliCookie> cookieStrFactory,
    ITaskDelay taskDelay
) : AppService
{
    /// <summary>
    /// 账号间随机间隔范围（秒）。多账号零间隔连续执行是服务端群控聚类的显著特征，
    /// 每个账号之间随机停顿以打散时间聚集。
    /// </summary>
    private static readonly int AccountIntervalMinSeconds = 15;

    private static readonly int AccountIntervalMaxSeconds = 90;

    public override async Task DoTaskAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "【账号个数】{count}个" + Environment.NewLine,
            cookieStrFactory.Count
        );
        var failedAccounts = new List<(int Index, Exception Exception)>();
        for (int i = 0; i < cookieStrFactory.Count; i++)
        {
            logger.LogInformation("######### 账号 {num} #########" + Environment.NewLine, i);
            try
            {
                var ck = cookieStrFactory.GetCookie(i);
                await DoTaskAccountAsync(ck, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                failedAccounts.Add((i, e));
                logger.LogWarning(e, "账号 {num} 执行失败", i);
            }

            if (i < cookieStrFactory.Count - 1)
            {
                var interval = TimeSpan.FromSeconds(
                    Random.Shared.Next(AccountIntervalMinSeconds, AccountIntervalMaxSeconds + 1)
                );
                logger.LogInformation("账号间随机间隔 {seconds} 秒后继续", interval.TotalSeconds);
                await taskDelay.Delay(interval, cancellationToken);
            }
        }

        if (failedAccounts.Count > 0)
        {
            throw new TaskExecutionException(
                $"{failedAccounts.Count} 个账号执行失败：{string.Join("；", failedAccounts.Select(x => $"账号{x.Index}：{x.Exception.Message}"))}",
                new AggregateException(failedAccounts.Select(x => x.Exception))
            );
        }
    }

    protected abstract Task DoTaskAccountAsync(
        BiliCookie ck,
        CancellationToken cancellationToken = default
    );
}
