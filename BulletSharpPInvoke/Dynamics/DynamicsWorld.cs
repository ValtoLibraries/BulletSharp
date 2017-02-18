using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using BulletSharp.Math;

namespace BulletSharp
{
	public enum DynamicsWorldType
	{
        Simple = 1,
        Discrete = 2,
        Continuous = 3,
        SoftRigid = 4,
        Gpu = 5
	}

	public abstract class DynamicsWorld : CollisionWorld
	{
        public delegate void InternalTickCallback(DynamicsWorld world, float timeStep);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        delegate void InternalTickCallbackUnmanaged(IntPtr world, float timeStep);

        private InternalTickCallback _preTickCallback;
        private InternalTickCallback _postTickCallback;
        private InternalTickCallbackUnmanaged _preTickCallbackUnmanaged;
        private InternalTickCallbackUnmanaged _postTickCallbackUnmanaged;
        protected ConstraintSolver _constraintSolver;
        ContactSolverInfo _solverInfo;

        Dictionary<IAction, ActionInterfaceWrapper> _actions;
        List<TypedConstraint> _constraints = new List<TypedConstraint>();

		internal DynamicsWorld(IntPtr native, Dispatcher dispatcher, BroadphaseInterface pairCache)
			: base(native, dispatcher, pairCache)
		{
		}

		public void AddAction(IAction action)
		{
            if (_actions == null)
            {
                _actions = new Dictionary<IAction, ActionInterfaceWrapper>();
            }
            else if (_actions.ContainsKey(action))
            {
                return;
            }

            ActionInterfaceWrapper wrapper = new ActionInterfaceWrapper(action, this);
            _actions.Add(action, wrapper);
            btDynamicsWorld_addAction(_native, wrapper._native);
		}

		public void AddConstraint(TypedConstraint constraint, bool disableCollisionsBetweenLinkedBodies = false)
		{
            _constraints.Add(constraint);
			btDynamicsWorld_addConstraint(_native, constraint._native, disableCollisionsBetweenLinkedBodies);

            if (disableCollisionsBetweenLinkedBodies)
            {
                RigidBody rigidBody = constraint.RigidBodyA;
                if (rigidBody._constraintRefs == null)
                {
                    rigidBody._constraintRefs = new List<TypedConstraint>();
                }
                rigidBody._constraintRefs.Add(constraint);

                rigidBody = constraint.RigidBodyB;
                if (rigidBody._constraintRefs == null)
                {
                    rigidBody._constraintRefs = new List<TypedConstraint>();
                }
                rigidBody._constraintRefs.Add(constraint);
            }
		}

		public void AddRigidBody(RigidBody body)
		{
            _collisionObjectArray.Add(body);
		}

        public void AddRigidBody(RigidBody body, CollisionFilterGroups group, CollisionFilterGroups mask)
		{
            _collisionObjectArray.Add(body, (int)group, (int)mask);
		}

        public void AddRigidBody(RigidBody body, int group, int mask)
        {
            _collisionObjectArray.Add(body, group, mask);
        }

		public void ClearForces()
		{
			btDynamicsWorld_clearForces(_native);
		}

		public TypedConstraint GetConstraint(int index)
		{
            System.Diagnostics.Debug.Assert(btDynamicsWorld_getConstraint(_native, index) == _constraints[index]._native);
            return _constraints[index];
		}

        public void GetGravity(out Vector3 gravity)
        {
            btDynamicsWorld_getGravity(_native, out gravity);
        }

		public void RemoveAction(IAction action)
		{
            if (_actions == null)
            {
                // No actions have been added
                return;
            }

            ActionInterfaceWrapper wrapper;
            if (_actions.TryGetValue(action, out wrapper))
            {
                btDynamicsWorld_removeAction(_native, wrapper._native);
                _actions.Remove(action);
                wrapper.Dispose();
            }
		}

		public void RemoveConstraint(TypedConstraint constraint)
		{
            RigidBody rigidBody = constraint.RigidBodyA;
            if (rigidBody._constraintRefs != null)
            {
                rigidBody._constraintRefs.Remove(constraint);
            }
            rigidBody = constraint.RigidBodyB;
            if (rigidBody._constraintRefs != null)
            {
                rigidBody._constraintRefs.Remove(constraint);
            }

            int itemIndex = _constraints.IndexOf(constraint);
            int lastIndex = _constraints.Count - 1;
            _constraints[itemIndex] = _constraints[lastIndex];
            _constraints.RemoveAt(lastIndex);
			btDynamicsWorld_removeConstraint(_native, constraint._native);
		}

		public void RemoveRigidBody(RigidBody body)
		{
            _collisionObjectArray.Remove(body);
		}

        public void SetGravity(ref Vector3 gravity)
        {
            btDynamicsWorld_setGravity(_native, ref gravity);
        }

        private void InternalPreTickCallbackNative(IntPtr world, float timeStep)
        {
            _preTickCallback(this, timeStep);
        }

        private void InternalPostTickCallbackNative(IntPtr world, float timeStep)
        {
            _postTickCallback(this, timeStep);
        }

        public void SetInternalTickCallback(InternalTickCallback callback, Object worldUserInfo = null,
            bool isPreTick = false)
        {
            if (isPreTick)
            {
                SetInternalPreTickCallback(callback);
            }
            else
            {
                SetInternalPostTickCallback(callback);
            }
            WorldUserInfo = worldUserInfo;
        }

        private void SetInternalPreTickCallback(InternalTickCallback callback)
        {
            if (_preTickCallback != callback)
            {
                _preTickCallback = callback;
                if (callback != null)
                {
                    if (_preTickCallbackUnmanaged == null)
                    {
                        _preTickCallbackUnmanaged = new InternalTickCallbackUnmanaged(InternalPreTickCallbackNative);
                    }
                    btDynamicsWorld_setInternalTickCallback(_native,
                        Marshal.GetFunctionPointerForDelegate(_preTickCallbackUnmanaged), IntPtr.Zero, true);
                }
                else
                {
                    _preTickCallbackUnmanaged = null;
                    btDynamicsWorld_setInternalTickCallback(_native, IntPtr.Zero, IntPtr.Zero, true);
                }
            }
        }

        private void SetInternalPostTickCallback(InternalTickCallback callback)
        {
            if (_postTickCallback != callback)
            {
                _postTickCallback = callback;
                if (callback != null)
                {
                    if (_postTickCallbackUnmanaged == null)
                    {
                        _postTickCallbackUnmanaged = new InternalTickCallbackUnmanaged(InternalPostTickCallbackNative);
                    }
                    btDynamicsWorld_setInternalTickCallback(_native,
                        Marshal.GetFunctionPointerForDelegate(_postTickCallbackUnmanaged), IntPtr.Zero, false);
                }
                else
                {
                    _postTickCallbackUnmanaged = null;
                    btDynamicsWorld_setInternalTickCallback(_native, IntPtr.Zero, IntPtr.Zero, false);
                }
            }
        }

		public int StepSimulation(float timeStep, int maxSubSteps = 1, float fixedTimeStep = 1.0f / 60.0f)
		{
			return btDynamicsWorld_stepSimulation(_native, timeStep, maxSubSteps,
				fixedTimeStep);
		}

		public void SynchronizeMotionStates()
		{
			btDynamicsWorld_synchronizeMotionStates(_native);
		}

		public ConstraintSolver ConstraintSolver
		{
            get
            {
                if (_constraintSolver == null)
                {
                    _constraintSolver = new SequentialImpulseConstraintSolver(btDynamicsWorld_getConstraintSolver(_native), true);
                }
                return _constraintSolver;
            }
            set
            {
                _constraintSolver = value;
                btDynamicsWorld_setConstraintSolver(_native, value._native);
            }
		}

		public Vector3 Gravity
		{
			get
			{
				Vector3 value;
				btDynamicsWorld_getGravity(_native, out value);
				return value;
			}
			set { btDynamicsWorld_setGravity(_native, ref value); }
		}

		public int NumConstraints
		{
			get { return btDynamicsWorld_getNumConstraints(_native); }
		}

		public ContactSolverInfo SolverInfo
		{
            get
            {
                if (_solverInfo == null)
                {
                    _solverInfo = new ContactSolverInfo(btDynamicsWorld_getSolverInfo(_native), true);
                }
                return _solverInfo;
            }
		}

		public DynamicsWorldType WorldType
		{
			get { return btDynamicsWorld_getWorldType(_native); }
		}

        public Object WorldUserInfo { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (_actions != null)
            {
                foreach (ActionInterfaceWrapper wrapper in _actions.Values)
                {
                    wrapper.Dispose();
                }
            }

            base.Dispose(disposing);
        }

		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_addAction(IntPtr obj, IntPtr action);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_addConstraint(IntPtr obj, IntPtr constraint, bool disableCollisionsBetweenLinkedBodies);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_clearForces(IntPtr obj);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern IntPtr btDynamicsWorld_getConstraint(IntPtr obj, int index);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern IntPtr btDynamicsWorld_getConstraintSolver(IntPtr obj);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_getGravity(IntPtr obj, out Vector3 gravity);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern int btDynamicsWorld_getNumConstraints(IntPtr obj);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern IntPtr btDynamicsWorld_getSolverInfo(IntPtr obj);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern DynamicsWorldType btDynamicsWorld_getWorldType(IntPtr obj);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_removeAction(IntPtr obj, IntPtr action);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_removeConstraint(IntPtr obj, IntPtr constraint);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_setConstraintSolver(IntPtr obj, IntPtr solver);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_setGravity(IntPtr obj, [In] ref Vector3 gravity);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_setInternalTickCallback(IntPtr obj, IntPtr cb, IntPtr worldUserInfo, bool isPreTick);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern int btDynamicsWorld_stepSimulation(IntPtr obj, float timeStep, int maxSubSteps, float fixedTimeStep);
		[DllImport(Native.Dll, CallingConvention = Native.Conv), SuppressUnmanagedCodeSecurity]
		static extern void btDynamicsWorld_synchronizeMotionStates(IntPtr obj);
	}
}
