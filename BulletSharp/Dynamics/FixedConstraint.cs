using BulletSharp.Math;
using static BulletSharp.UnsafeNativeMethods;

namespace BulletSharp
{
	public class FixedConstraint : Generic6DofSpring2Constraint
	{
		public FixedConstraint(RigidBody rigidBodyA, RigidBody rigidBodyB, Matrix frameInA,
			Matrix frameInB)
			: base(btFixedConstraint_new(rigidBodyA.Native, rigidBodyB.Native,
				ref frameInA, ref frameInB))
		{
			_rigidBodyA = rigidBodyA;
			_rigidBodyB = rigidBodyB;
		}
	}
}
