#ifndef UNIVERSAL_IMAGE_BASED_LIGHTING_INCLUDED
#define UNIVERSAL_IMAGE_BASED_LIGHTING_INCLUDED

real3 ComputeSphereNormal(real2 coord, real phiStart = 0.0, real phiLength = PI* 2, real thetaStart = 0, real thetaLength = PI)
{
    real3 normal;
    normal.x = -sin(thetaStart + coord.y * thetaLength) * sin(phiStart + coord.x * phiLength);
    normal.y = -cos(thetaStart + coord.y * thetaLength);
    normal.z = -sin(thetaStart + coord.y * thetaLength) * cos(phiStart + coord.x * phiLength);
    return normal;
}

real VanDerCorpus(uint n, uint base)
{
    real invBase = 1.0 / real(base);
    real denom   = 1.0;
    real result  = 0.0;

    for (uint i = 0u; i < 32u; ++i)
    {
        if (n > 0u)
        {
            denom   = real(n) % 2.0;
            result += denom * invBase;
            invBase = invBase / 2.0;
            n = uint(real(n) / 2.0);
        }
    }

    return result;
}

uint ReverseBits32(uint bits)
{
    bits = (bits << 16) | ( bits >> 16);
    bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
    bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
    bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
    bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
    return bits;
}

real2 Hammersley(uint i, uint samplesCount)
{
    real E1 = (real)i / samplesCount;
    real E2 = ReverseBits32(i) * 2.3283064365386963e-10;
    return real2(E1, E2);
}

real2 HammersleyNoBitOps(uint i, uint samplesCount)
{
    real E1 = (real)i / samplesCount;
    return real2(E1, VanDerCorpus(i, 2u));
}

real2 Hammersley(uint i, uint samplesCount, int2 random)
{
    real E1 = frac((real)i / samplesCount + real(random.x & 0xffff) / (1 << 16));
    real E2 = real(ReverseBits32(i) ^ random.y) * 2.3283064365386963e-10;
    return real2(E1, E2);
}

real3 HammersleySampleCos(real2 Xi)
{
    real phi = 2 * PI * Xi.x;

    real cosTheta = sqrt(Xi.y);
    real sinTheta = sqrt(1 - cosTheta * cosTheta);

    real3 H;
    H.x = sinTheta * cos(phi);
    H.y = sinTheta * sin(phi);
    H.z = cosTheta;

    return H;
}

real3 HammersleySampleGGX(real2 Xi, real roughness)
{
    real m = roughness * roughness;
    real m2 = m * m;
    real u = (1 - Xi.y) / (1 + (m2 - 1) * Xi.y);

    return HammersleySampleCos(real2(Xi.x, u));
}

real4 CosineSampleHemisphere( real2 E )
{
	real Phi = 2 * PI * E.x;
	real CosTheta = sqrt( E.y );
	real SinTheta = sqrt( 1 - CosTheta * CosTheta );

	real3 H;
	H.x = SinTheta * cos( Phi );
	H.y = SinTheta * sin( Phi );
	H.z = CosTheta;

	real PDF = CosTheta * (1.0 /  PI);

	return real4( H, PDF );
}

real3 TangentToWorld(real3 N, real3 H)
{
    real3 TangentY = abs(N.z) < 0.999 ? real3(0,0,1) : real3(1,0,0);
    real3 TangentX = normalize(cross(TangentY, N));
    return TangentX * H.x + cross(N, TangentX) * H.y + N * H.z;
}

real DistributionGGX(real3 N, real3 H, real roughness)
{
    real a = roughness * roughness;
    real a2 = a*a;
    real NdotH = max(dot(N, H), 0.0);
    real NdotH2 = NdotH*NdotH;

    real nom   = a2;
    real denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

real3 ImportanceSampleSpecularIBL(TEXTURECUBE_PARAM(CubeMap, sampler_CubeMap), real3 N, real3 V, real3 f0, real roughness, int2 random, real resolution)
{
    const uint NumSamples = 32;

    real3 lighting = 0;
    real weight = 0;

    for (uint i = 0; i < NumSamples; i++)
    {
        real2 E = Hammersley(i, NumSamples, random);
        real3 H = TangentToWorld(N, HammersleySampleGGX(E, roughness));
        real3 L = 2 * dot(V, H) * H - V;

        real nl = saturate(dot(N, L));
        if (nl > 0)
        {
            real nh = saturate(dot(N, H));
            real vh = saturate(dot(V, H));

            real D = DistributionGGX(N, H, roughness);
            real pdf = D * nh / (4.0 * vh) + 0.0001;

            real saTexel = 4.0f * PI / (6.0f * resolution * resolution);
            real saSample = 1.0f / (NumSamples * pdf + 0.00001f);
            real mipLevel = roughness == 0.0f ? 0.0f : 0.5f * log2(saSample / saTexel);

            lighting += SAMPLE_TEXTURECUBE_LOD(CubeMap, sampler_CubeMap, L, mipLevel).rgb * nl;
            weight += nl;
        }
    }

    return lighting / max(0.001f, weight);
}

#endif