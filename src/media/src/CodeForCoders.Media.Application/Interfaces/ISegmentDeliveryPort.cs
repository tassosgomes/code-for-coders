namespace CodeForCoders.Media.Application.Interfaces;

public interface ISegmentDeliveryPort
{
    SegmentDelivery CreateSegmentAccess(string videoPrefix, DateTimeOffset expiresAt);
}
