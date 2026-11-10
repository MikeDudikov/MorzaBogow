import pathlib
import unittest

from asset_pipeline.blender import output_file


class BlenderOutputTests(unittest.TestCase):
    def test_windows_blender_paths_map_to_the_job_folder(self):
        # Blender runs on Windows and reports C:\... paths while the pipeline runs in WSL (first live run, 2026-11-10).
        out = pathlib.Path("/mnt/c/Users/x/MorzaBogow/ArtWorkspace/jobs/job/geometry")
        reported = "C:\\Users\\x\\MorzaBogow\\ArtWorkspace\\jobs\\job\\geometry\\asset_LOD0.fbx"
        self.assertEqual(output_file(out, reported), out / "asset_LOD0.fbx")

    def test_posix_paths_still_map_to_the_job_folder(self):
        out = pathlib.Path("/tmp/job/geometry")
        self.assertEqual(output_file(out, "/tmp/job/geometry/asset_LOD1.fbx"), out / "asset_LOD1.fbx")


if __name__ == "__main__":
    unittest.main()
