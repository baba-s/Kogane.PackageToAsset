using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Kogane.Internal
{
    internal static class PackageToAsset
    {
        private const string MENU_ITEM_NAME_ROOT      = "Assets/Package to Asset/";
        private const string MENU_ITEM_NAME_INCLUDING = MENU_ITEM_NAME_ROOT + "Including Dependencies";
        private const string MENU_ITEM_NAME_EXCLUDING = MENU_ITEM_NAME_ROOT + "Excluding Dependencies";

        /// <summary>
        /// Package を Assets フォルダに移動できる場合 true を返します<br />
        /// 選択されているアセットが Package ではない場合 false を返します<br />
        /// </summary>
        [MenuItem( MENU_ITEM_NAME_INCLUDING, true )]
        [MenuItem( MENU_ITEM_NAME_EXCLUDING, true )]
        private static bool CanMove()
        {
            return GetSelectedPackageInfo() != null;
        }

        /// <summary>
        /// 依存関係も含めて選択された Package を Assets フォルダに移動します
        /// </summary>
        [MenuItem( MENU_ITEM_NAME_INCLUDING )]
        private static void MoveIncludingDependencies()
        {
            // Project ウィンドウで選択中の Package の情報を取得します
            var selectedPackageInfo = GetSelectedPackageInfo();

            if ( selectedPackageInfo == null ) return;

            var packageInfos = selectedPackageInfo.dependencies
                    // Built-in の Package は Assets フォルダに移動できないので除外します
                    .Where( x => !x.name.StartsWith( "com.unity.modules." ) )
                    .Select( x => PackageInfo.FindForAssetPath( $"Packages/{x.name}" ) )
                    .Where( x => x != null )
                    .Append( selectedPackageInfo )
                    .ToArray()
                ;

            MovePackageToAsset( packageInfos );
        }

        /// <summary>
        /// 依存関係は含まず選択された Package のみを Assets フォルダに移動します
        /// </summary>
        [MenuItem( MENU_ITEM_NAME_EXCLUDING )]
        private static void MoveExcludingDependencies()
        {
            // Project ウィンドウで選択中の Package の情報を取得します
            var selectedPackageInfo = GetSelectedPackageInfo();

            if ( selectedPackageInfo == null ) return;

            MovePackageToAsset( selectedPackageInfo );
        }

        /// <summary>
        /// Project ウィンドウで選択中の Package の情報を返します<br />
        /// Package が選択されていない場合は null を返します<br />
        /// </summary>
        [CanBeNull]
        private static PackageInfo GetSelectedPackageInfo()
        {
            var activeObject        = Selection.activeObject;
            var assetPath           = AssetDatabase.GetAssetPath( activeObject );
            var selectedPackageInfo = PackageInfo.FindForAssetPath( assetPath );

            return selectedPackageInfo;
        }

        /// <summary>
        /// 指定されたすべての Package を Assets フォルダに移動します
        /// </summary>
        private static async void MovePackageToAsset( [NotNull][ItemNotNull] params PackageInfo[] packageInfos )
        {
            AssetDatabase.StartAssetEditing();

            try
            {
                var count            = packageInfos.Length;
                var currentDirectory = $@"{Directory.GetCurrentDirectory()}\";

                for ( var i = 0; i < count; i++ )
                {
                    var packageInfo = packageInfos[ i ];
                    var directory   = packageInfo.resolvedPath.Replace( currentDirectory, "" );
                    var folderName  = Path.GetFileName( directory );
                    var number      = i + 1;

                    EditorUtility.DisplayProgressBar
                    (
                        title: "Package to Asset",
                        info: $"{number}/{count} {packageInfo.name}",
                        progress: ( float ) i / count
                    );

                    File.Move( directory, $"Assets/{folderName}" );

                    await RemoveAsync( packageInfo );
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 指定された Package を Package Manager から Remove します
        /// </summary>
        private static Task RemoveAsync( PackageInfo packageInfo )
        {
            var tcs     = new TaskCompletionSource<bool>();
            var request = Client.Remove( packageInfo.name );

            EditorApplication.update += OnUpdate;

            void OnUpdate()
            {
                if ( !request.IsCompleted ) return;

                EditorApplication.update -= OnUpdate;
                tcs.TrySetResult( true );
            }

            return tcs.Task;
        }
    }
}