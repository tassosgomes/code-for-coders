# Media

`media` owns the boundary between the product and media providers. Application exposes only the
vendor-neutral `IMediaStoragePort` and `IMediaCdnPort`; the S3 and CloudFront adapters live in
`Infra.Data/Adapters`.

`AwsMedia` contains region, bucket, object-prefix and distribution-domain configuration only. No
AWS access key, secret, private key or signed-URL material is committed. Production credentials are
resolved through the AWS default credential chain/workload identity and secret management outside
the service configuration.

The current foundation slice returns deterministic S3/CloudFront references without making a network
call. Replacing that implementation with AWS SDK calls is intentionally isolated to the adapters.
