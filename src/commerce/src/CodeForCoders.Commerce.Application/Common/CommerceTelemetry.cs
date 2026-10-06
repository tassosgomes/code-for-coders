using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Commerce.Application.Common;

public static class CommerceTelemetry
{
    public const string ServiceName = "CodeForCoders.Commerce";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly System.Diagnostics.Metrics.Counter<long> PaymentAmountMismatches = Meter.CreateCounter<long>("commerce.sales.payment.amount-mismatches", "{payment}");
    public static readonly System.Diagnostics.Metrics.Histogram<double> PaymentAccessLag = Meter.CreateHistogram<double>("commerce.entitlement.payment-access-lag", "s");
    public static readonly Gauge<long> PaidOrdersWithoutAccess = Meter.CreateGauge<long>("commerce.sales.payment.access-overdue", "{order}");
    public static readonly System.Diagnostics.Metrics.Counter<long> PurchaseDeadLetters = Meter.CreateCounter<long>("commerce.purchase.dead-letters", "{delivery}");
    public static readonly Gauge<double> AccessExpirationLag = Meter.CreateGauge<double>("commerce.entitlement.expiration.lag", "s");
    public static readonly Counter<long> CourseFactsApplied = Meter.CreateCounter<long>("commerce.catalog.course.applied", "{fact}");
    public static readonly Counter<long> CourseFactsIgnored = Meter.CreateCounter<long>("commerce.catalog.course.ignored", "{fact}");
    public static readonly Counter<long> CourseFactsDeadLettered = Meter.CreateCounter<long>("commerce.catalog.course.dead_lettered", "{fact}");
    public static readonly Counter<long> EntitlementCourseFactsApplied = Meter.CreateCounter<long>("commerce.entitlement.course.applied", "{fact}");
    public static readonly Counter<long> EntitlementCourseFactsIgnored = Meter.CreateCounter<long>("commerce.entitlement.course.ignored", "{fact}");
    public static readonly Counter<long> EntitlementCourseFactsDeadLettered = Meter.CreateCounter<long>("commerce.entitlement.course.dead_lettered", "{fact}");
    public static readonly Histogram<double> EntitlementCourseFactLag = Meter.CreateHistogram<double>("commerce.entitlement.course.lag", "s");
    public static readonly Counter<long> PurchaseIntentsCounted = Meter.CreateCounter<long>("commerce.purchase_intent.counted", "{click}");
    public static readonly Counter<long> PurchaseIntentsRepeated = Meter.CreateCounter<long>("commerce.purchase_intent.repeated", "{click}");
    public static readonly Counter<long> PurchaseIntentsRefused = Meter.CreateCounter<long>("commerce.purchase_intent.refused", "{click}");
    public static readonly Counter<long> ShowcaseReads = Meter.CreateCounter<long>("commerce.showcase.read", "{read}");
    public static readonly Histogram<double> CourseFactLag = Meter.CreateHistogram<double>("commerce.catalog.course.lag", "s");
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
