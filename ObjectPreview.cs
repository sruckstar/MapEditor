using GTA;
using LemonUI.Scaleform;

namespace MapEditor
{
    public static class ObjectPreview
    {
        private static InstructionalButtons _loadingButtons;

        public static Model LoadObject(int hash)
        {
            int counter = 0;

            var m = new Model(hash);

	        if (!m.IsValid || !m.IsInCdImage)
	        {
		        if (!ObjectDatabase.InvalidHashes.Contains(hash))
		        {
			        ObjectDatabase.InvalidHashes.Add(hash);
					ObjectDatabase.SaveInvalidHashes();
		        }
		        return null;
	        }

            if (_loadingButtons == null)
            {
                _loadingButtons = new InstructionalButtons(
                    new InstructionalButton(Translation.Translate("Loading Model"), "b_50"));
                _loadingButtons.Update();
            }

            while (!m.IsLoaded && counter < 200)
			{
                m.Request();
                Script.Yield();
                counter++;
                _loadingButtons.Draw();
            }
            return m;
        }
    }
}
