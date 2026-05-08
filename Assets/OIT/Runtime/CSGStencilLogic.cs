namespace OIT
{
    // CPU state machine mirroring the GPU stencil increment/decrement behavior for a single pixel.
    // Back-face depth pass → increment; front-face depth pass → decrement.
    // IsInsideCutter() returns true when the net stencil value is positive.
    public class CSGStencilLogic
    {
        private int _stencilValue;

        public int StencilValue => _stencilValue;

        public void OnBackFaceDepthPass() => _stencilValue++;

        public void OnFrontFaceDepthPass() => _stencilValue--;

        public bool IsInsideCutter() => _stencilValue > 0;
    }
}
