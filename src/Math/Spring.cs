namespace MyGame;

// Cached set of motion parameters that can be used to efficiently update
// multiple springs using the same time step, angular frequency and damping
// ratio.
public struct DampedSpringMotionParams
{
    // newPos = posPosCoef*oldPos + posVelCoef*oldVel
    public float PosPosCoef;
    public float PosVelCoef;
    public float VelPosCoef;
    public float VelVelCoef;
}

[CustomInspector(typeof(GroupInspector))]
public class Spring
{
    private bool _motionParamsDirty = true;
    public DampedSpringMotionParams MotionParams = new();

    private float _angularFrequency = 15f;

    public float AngularFrequency
    {
        get => _angularFrequency;
        set
        {
            _angularFrequency = value;
            _motionParamsDirty = true;
        }
    }

    private float _dampingRatio = 0.6f;

    public float DampingRatio
    {
        get => _dampingRatio;
        set
        {
            _dampingRatio = value;
            _motionParamsDirty = true;
        }
    }

    public float Position;
    public float Velocity;

    public float EquilibriumPosition = 0;

    public void Update(float deltaSeconds)
    {
        if (_motionParamsDirty)
        {
            CalcDampedSpringMotionParams(out MotionParams, deltaSeconds, AngularFrequency, DampingRatio);
            _motionParamsDirty = false;
        }

        UpdateDampedSpringMotion(ref Position, ref Velocity, EquilibriumPosition, MotionParams);

        if (MathF.IsNearZero(Velocity, 0.001f))
        {
            Velocity = 0;
        }

        if (MathF.IsNearZero(Position, 0.001f))
        {
            Position = 0;
        }
    }

    /// <summary>
    /// This function will compute the parameters needed to simulate a damped spring
    /// over a given period of time.
    /// - An angular frequency is given to control how fast the spring oscillates.
    /// - A damping ratio is given to control how fast the motion decays.
    ///     damping ratio > 1: over damped
    ///     damping ratio = 1: critically damped
    ///     damping ratio < 1: under damped
    /// 
    /// https://www.ryanjuckett.com/damped-springs/
    /// </summary>
    /// <param name="pOutParams">motion parameters result</param>
    /// <param name="deltaTime">time step to advance</param>
    /// <param name="angularFrequency">angular frequency of motion</param>
    /// <param name="dampingRatio">damping ratio of motion</param>
    public static void CalcDampedSpringMotionParams(out DampedSpringMotionParams pOutParams, float deltaTime, float angularFrequency, float dampingRatio)
    {
        // force values into legal range
        if (dampingRatio < 0.0f) dampingRatio = 0.0f;
        if (angularFrequency < 0.0f) angularFrequency = 0.0f;

        // if there is no angular frequency, the spring will not move and we can
        // return identity
        if (angularFrequency < MathF.Epsilon)
        {
            pOutParams.PosPosCoef = 1.0f;
            pOutParams.PosVelCoef = 0.0f;
            pOutParams.VelPosCoef = 0.0f;
            pOutParams.VelVelCoef = 1.0f;
            return;
        }

        if (dampingRatio > 1.0f + MathF.Epsilon)
        {
            // over-damped
            var za = -angularFrequency * dampingRatio;
            var zb = angularFrequency * MathF.Sqrt(dampingRatio * dampingRatio - 1.0f);
            var z1 = za - zb;
            var z2 = za + zb;

            var e1 = MathF.Exp(z1 * deltaTime);
            var e2 = MathF.Exp(z2 * deltaTime);

            var invTwoZb = 1.0f / (2.0f * zb); // = 1 / (z2 - z1)

            var e1_Over_TwoZb = e1 * invTwoZb;
            var e2_Over_TwoZb = e2 * invTwoZb;

            var z1e1_Over_TwoZb = z1 * e1_Over_TwoZb;
            var z2e2_Over_TwoZb = z2 * e2_Over_TwoZb;

            pOutParams.PosPosCoef = e1_Over_TwoZb * z2 - z2e2_Over_TwoZb + e2;
            pOutParams.PosVelCoef = -e1_Over_TwoZb + e2_Over_TwoZb;

            pOutParams.VelPosCoef = (z1e1_Over_TwoZb - z2e2_Over_TwoZb + e2) * z2;
            pOutParams.VelVelCoef = -z1e1_Over_TwoZb + z2e2_Over_TwoZb;
        }
        else if (dampingRatio < 1.0f - MathF.Epsilon)
        {
            // under-damped
            var omegaZeta = angularFrequency * dampingRatio;
            var alpha = angularFrequency * MathF.Sqrt(1.0f - dampingRatio * dampingRatio);

            var expTerm = MathF.Exp(-omegaZeta * deltaTime);
            var cosTerm = MathF.Cos(alpha * deltaTime);
            var sinTerm = MathF.Sin(alpha * deltaTime);

            var invAlpha = 1.0f / alpha;

            var expSin = expTerm * sinTerm;
            var expCos = expTerm * cosTerm;
            var expOmegaZetaSin_Over_Alpha = expTerm * omegaZeta * sinTerm * invAlpha;

            pOutParams.PosPosCoef = expCos + expOmegaZetaSin_Over_Alpha;
            pOutParams.PosVelCoef = expSin * invAlpha;

            pOutParams.VelPosCoef = -expSin * alpha - omegaZeta * expOmegaZetaSin_Over_Alpha;
            pOutParams.VelVelCoef = expCos - expOmegaZetaSin_Over_Alpha;
        }
        else
        {
            // critically damped
            var expTerm = MathF.Exp(-angularFrequency * deltaTime);
            var timeExp = deltaTime * expTerm;
            var timeExpFreq = timeExp * angularFrequency;

            pOutParams.PosPosCoef = timeExpFreq + expTerm;
            pOutParams.PosVelCoef = timeExp;

            pOutParams.VelPosCoef = -angularFrequency * timeExpFreq;
            pOutParams.VelVelCoef = -timeExpFreq + expTerm;
        }
    }

    /// <summary>
    // This function will update the supplied position and velocity values over
    // according to the motion parameters.
    /// </summary>
    /// <param name="position">position value to update</param>
    /// <param name="velocity">velocity value to update</param>
    /// <param name="equilibriumPosition">position to approach</param>
    /// <param name="motionParams">motion parameters to use</param>
    public static void UpdateDampedSpringMotion(ref float position, ref float velocity, float equilibriumPosition, in DampedSpringMotionParams motionParams)
    {
        var oldPos = position - equilibriumPosition; // update in equilibrium relative space
        var oldVel = velocity;

        position = oldPos * motionParams.PosPosCoef + oldVel * motionParams.PosVelCoef + equilibriumPosition;
        velocity = oldPos * motionParams.VelPosCoef + oldVel * motionParams.VelVelCoef;
    }
}
