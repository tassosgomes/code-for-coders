import crypto from 'crypto';

function date() {
    return new Date().toISOString().replace(/[:-]|\.\d{3}/g, '');
}

function hmac(key, value) {
    return crypto.createHmac('sha256', key).update(value).digest();
}

// Authenticate the origin read while keeping the bucket private.
function authorization(r) {
    const timestamp = r.variables.origin_date;
    const day = timestamp.slice(0, 8);
    const scope = day + '/' + r.variables.origin_region + '/s3/aws4_request';
    const headers = 'host:' + r.variables.origin_host + '\n' +
        'x-amz-content-sha256:UNSIGNED-PAYLOAD\n' + 'x-amz-date:' + timestamp + '\n';
    const signedHeaders = 'host;x-amz-content-sha256;x-amz-date';
    const canonical = r.method + '\n' + r.variables.origin_path + '\n\n' +
        headers + '\n' + signedHeaders + '\nUNSIGNED-PAYLOAD';
    const stringToSign = 'AWS4-HMAC-SHA256\n' + timestamp + '\n' + scope + '\n' +
        crypto.createHash('sha256').update(canonical).digest('hex');
    let key = hmac('AWS4' + r.variables.origin_secret_key, day);
    key = hmac(key, r.variables.origin_region);
    key = hmac(key, 's3');
    key = hmac(key, 'aws4_request');
    const signature = crypto.createHmac('sha256', key).update(stringToSign).digest('hex');
    return 'AWS4-HMAC-SHA256 Credential=' + r.variables.origin_access_key + '/' + scope +
        ', SignedHeaders=' + signedHeaders + ', Signature=' + signature;
}

export default { date, authorization };
