use std::env;
use std::fs;

use std::io::{self, Write, BufReader, BufRead, Seek, SeekFrom};
use std::path::{Path, PathBuf};

use std::process::Command;
use std::thread::sleep;
use std::time::Duration;

use std::sync::{Arc, Mutex};
use std::sync::atomic::{AtomicBool, Ordering};

use regex::Regex;
use serde::{Serialize, Deserialize};

use winreg::enums::*;
use winreg::RegKey;

use windows::Win32::Security::TOKEN_QUERY;
use windows::Win32::Foundation::CloseHandle;
use windows::Win32::System::Threading::{GetCurrentProcess, OpenProcessToken};
use windows::Win32::Security::{GetTokenInformation, TOKEN_ELEVATION, TokenElevation};

use ctrlc;

fn fatal_error(message: &str, err: impl std::fmt::Display) -> !
{
	println!("ERROR: {}\n{}", message, err);
	print!("\nPress enter to exit ... ");
	io::stdout().flush().unwrap();
	let mut input = String::new();
	io::stdin().read_line(&mut input).unwrap();
	std::process::exit(1);
}

#[derive(Serialize, Deserialize, Debug)]
struct Module
{
	#[serde(rename = "IsEnabled")]
	is_enabled: bool,
	#[serde(rename = "Name")]
	name: String,
	#[serde(rename = "Version")]
	version: String,
	#[serde(rename = "Assemblies")]
	assemblies: Vec<Assembly>,
}

#[derive(Serialize, Deserialize, Debug)]
struct Assembly
{
	#[serde(rename = "Path")]
	path: String,
	#[serde(rename = "Role")]
	role: String,
	#[serde(rename = "Load_As_Byte_Array")]
	load_as_byte_array: bool,
}

struct CleanupData
{
	mdl_path: PathBuf,
	be_ini_path: PathBuf,
}

fn main()
{
	let base_path = match env::current_exe()
	{
		Ok(exe_path) => match exe_path.parent()
		{
			Some(parent) => parent.to_path_buf(),
			None => fatal_error("Failed to get base path", "Could not get parent directory"),
		},
		Err(e) => fatal_error("Failed to get base path", e),
	};

	let os_platform = env::consts::OS;
	println!("Detected operating system: {}", os_platform);

	if os_platform != "windows"
	{
		fatal_error("Unsupported operating system.", "This program only supports Windows");
	}

	println!("Verifying administrator privileges...");

	if !is_admin()
	{
		fatal_error("This program must be run as an administrator.", "Insufficient privileges");
	}

	println!("Successfully verified administrator privileges.");

	println!("Locating unturned game files...");

	let unt_path = match find_unturned_game_path()
	{
		Ok(path) => path,
		Err(e) => fatal_error("Failed to find Unturned game files", e),
	};

	println!("Located Unturned game files: {}", unt_path.display());

	let mdl_path = unt_path.join("Modules").join("SkinsModule");
	let be_ini_path = unt_path.join("BattlEye").join("BELauncher.ini");

	let cleanup_data = Arc::new(Mutex::new(CleanupData {
		mdl_path: mdl_path.clone(),
		be_ini_path: be_ini_path.clone(),
	}));

	let running = Arc::new(AtomicBool::new(true));
	let r = running.clone();
	let cleanup_for_signal = cleanup_data.clone();
	
	ctrlc::set_handler(move || {
		println!("\nIntercepted CTRL+C, performing cleanup...");
		
		if let Ok(data) = cleanup_for_signal.lock() {
			if is_unturned_running() {
				println!("Terminating Unturned process...");
				let _ = Command::new("taskkill")
					.args(&["/F", "/IM", "Unturned.exe"])
					.output();
				
				sleep(Duration::from_secs(2));
			}
			
			println!("Destroying module...");
			if directory_exists(&data.mdl_path) {
				if let Err(e) = fs::remove_dir_all(&data.mdl_path) {
					println!("Warning: Failed to destroy module: {}", e);
				} else {
					println!("Successfully destroyed module.");
				}
			}
			
			println!("Re-enabling BattlEye...");
			if file_exists(&data.be_ini_path) {
				match fs::read_to_string(&data.be_ini_path) {
					Ok(be_content) => {
						let modified_lines: Vec<String> = be_content
							.lines()
							.map(|line| {
								if line.starts_with("BEArg=") {
									"BEArg=-BattlEye".to_string()
								} else {
									line.to_string()
								}
							})
							.collect();
						
						let modified_content = modified_lines.join("\n");
						
						if let Err(e) = fs::write(&data.be_ini_path, modified_content) {
							println!("Warning: Failed to re-enable BattlEye: {}", e);
						} else {
							println!("Successfully re-enabled BattlEye.");
						}
					},
					Err(e) => println!("Warning: Failed to read BattlEye configuration: {}", e),
				}
			}
		}
		
		println!("Cleanup completed. Exiting...");
		r.store(false, Ordering::SeqCst);
		std::process::exit(0);
	}).expect("Error setting Ctrl-C handler");

	if !unt_path.is_dir()
	{
		fatal_error("Unturned game files path is not a directory.", "Invalid path");
	}

	println!("Successfully located Unturned directory.");

	let temp_file = unt_path.join("temp_permission_check");
	match fs::File::create(&temp_file)
	{
		Ok(file) => drop(file),
		Err(e) => fatal_error("Missing permissions to access the unturned directory.", e),
	}
	let _ = fs::remove_file(temp_file);

	println!("Successfully verified permissions.");

	if !directory_exists(&mdl_path)
	{
		println!("Creating module directory...");
		if let Err(e) = fs::create_dir_all(&mdl_path)
		{
			fatal_error("Failed to create module directory", e);
		}
	}

	println!("Writing binary .dll file to module...");

	let bin_path = mdl_path.join("bin");
	if !directory_exists(&bin_path)
	{
		let src_bin_path = base_path.join("bin");
		if !directory_exists(&src_bin_path) || !file_exists(&src_bin_path.join("SkinsModule.dll"))
		{
			fatal_error("No binary detected to copy.", "Missing binary files");
		}

		match copy_dir(&src_bin_path, &bin_path)
		{
			Ok(_) => {},
			Err(e) => fatal_error(&format!("Failed to write binary .dll file to module: {}", e), e),
		}

		println!("Successfully wrote binary .dll file.");
	}
	else
	{
		println!("Found existing binary .dll file. Skipping...");
	}

	println!("Writing language .dat file...");

	let dat_path = mdl_path.join("English.dat");
	let mut dat_file = match fs::File::create(&dat_path)
	{
		Ok(file) => file,
		Err(e) => fatal_error("Failed to create language file", e),
	};

	match dat_file.write_all(b"Name SkinsModule\nDescription Module which modifies skins.")
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to write language file content", e),
	}

	println!("Successfully wrote language .dat file.");

	println!("Writing .module json file...");

	let module = Module
	{
		is_enabled: true,
		name: "SkinsModule".to_string(),
		version: "1.0.0.0".to_string(),
		assemblies: vec![
			Assembly
			{
				path: "/bin/SkinsModule.dll".to_string(),
				role: "Both_Optional".to_string(),
				load_as_byte_array: false,
			},
			Assembly
			{
				path: "/bin/0Harmony.dll".to_string(),
				role: "Both_Optional".to_string(),
				load_as_byte_array: false,
			},
		],
	};

	let module_json = match serde_json::to_string_pretty(&module)
	{
		Ok(json) => json,
		Err(e) => fatal_error("Failed to create module JSON", e),
	};

	println!("Module data:\n {}", module_json);

	println!("Verifying assemblies...");

	for asm in &module.assemblies
	{
		let rel_path = asm.path.trim_start_matches('/');
		let asm_path = mdl_path.join(rel_path);

		println!("Checking for assembly at: {}", asm_path.display());

		if !file_exists(&asm_path)
		{
			fatal_error(&format!("Failed to find assembly for {} at {}", 
				asm.path, asm_path.display()), "Missing assembly");
		}
	}

	println!("Successfully verified assemblies.");

	println!("Writing content...");

	let module_file_path = mdl_path.join("SkinsModule.module");
	match fs::write(&module_file_path, module_json)
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to write module file content", e),
	}

	println!("Successfully wrote .module json file.");
	println!("Successfully setup the module.");

	println!("Launching Unturned (without BattlEye)...");
	println!("Disabling BattlEye...");

	let be_content = match fs::read_to_string(&be_ini_path)
	{
		Ok(content) => content,
		Err(e) => fatal_error("Failed to read BattlEye configuration", e),
	};

	println!("BattlEye launch configuration:\n{}", be_content);

	println!("Modifying sys argv argument...");

	let modified_lines: Vec<String> = be_content
		.lines()
		.map(|line| {
			if line.starts_with("BEArg=") {
				"BEArg=".to_string()
			} else {
				line.to_string()
			}
		})
		.collect();

	let modified_content = modified_lines.join("\n");

	println!("Replaced BEArg to be NULL.");

	match fs::write(&be_ini_path, modified_content)
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to write modified BattlEye configuration", e),
	}

	println!("Successfully disabled BattlEye.");

	let cmd = [
		"C:\\Program Files (x86)\\Steam\\Steam.exe",
		"-applaunch",
		"304930",
	];

	println!("Executing command: {}", cmd.join(" "));

	println!("Launching...");

	match Command::new(&cmd[0]).args(&cmd[1..]).spawn()
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to launch Unturned", e),
	};

	println!("Successfully launched session.");

	sleep(Duration::from_secs(10));

	println!("Awaiting client shutdown...");
	println!("DO NOT CLOSE THIS WINDOW.");
	println!("Use Ctrl + C or close Unturned instead.");
	println!("Monitoring logs...");

	let log_path = unt_path.join("Logs").join("Client.log");
	monitor_unturned_and_logs(&log_path, running.clone());

	println!("Detected client shutdown.");

	sleep(Duration::from_secs(3));

	println!("Destroying module...");

	match fs::remove_dir_all(&mdl_path)
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to destroy module", e),
	}

	println!("Successfully destroyed module.");

	println!("Re-enabling BattlEye...");

	let be_content = match fs::read_to_string(&be_ini_path)
	{
		Ok(content) => content,
		Err(e) => fatal_error("Failed to read BattlEye configuration", e),
	};

	println!("Modifying sys argv argument...");

	let modified_lines: Vec<String> = be_content
		.lines()
		.map(|line| {
			if line.starts_with("BEArg=") {
				"BEArg=-BattlEye".to_string()
			} else {
				line.to_string()
			}
		})
		.collect();

	let modified_content = modified_lines.join("\n");

	println!("Replaced BEArg to be -BattlEye.");

	match fs::write(&be_ini_path, modified_content)
	{
		Ok(_) => {},
		Err(e) => fatal_error("Failed to write modified BattlEye configuration", e),
	}

	println!("\nFinished.");
}

fn monitor_unturned_and_logs(log_path: &Path, running: Arc<AtomicBool>) 
{
	while !is_unturned_running() && running.load(Ordering::SeqCst) 
	{
		println!("Waiting for client to start...");
		sleep(Duration::from_secs(2));
	}
	
	if !running.load(Ordering::SeqCst) 
	{
		return;
	}
	
	println!("Unturned is now running");
	println!("Waiting for logs to be accessible...");
	
	sleep(Duration::from_secs(5));
	
	let re = Regex::new(r"\[.*?\] \[SKIN MODULE\] (.+)").unwrap();
	
	let mut last_position: u64 = 0;
	let mut first_check = true;
	
	while is_unturned_running() && running.load(Ordering::SeqCst) 
	{
		if !file_exists(log_path) 
		{
			println!("Waiting Unturned logs to load...");
			sleep(Duration::from_secs(1));
			continue;
		}
		
		match fs::File::open(log_path) 
		{
			Ok(file) => 
			{
				let mut reader = BufReader::new(file);
				
				if first_check 
				{
					if let Ok(metadata) = fs::metadata(log_path) 
					{
						last_position = metadata.len();
						let _ = reader.seek(SeekFrom::Start(last_position));
						first_check = false;
						println!("Logs found, monitoring for new entries...");
						sleep(Duration::from_secs(1));
						continue;
					}
				} 
				else 
				{
					let _ = reader.seek(SeekFrom::Start(last_position));
				}
				
				let mut buffer = String::new();
				while let Ok(bytes_read) = reader.read_line(&mut buffer) 
				{
					if bytes_read == 0 
					{
						break;
					}
					
					if buffer.contains("[SKIN MODULE]") 
					{
						if let Some(captures) = re.captures(&buffer) 
						{
							if let Some(message) = captures.get(1) 
							{
								println!("{}", message.as_str().trim());
								let _ = io::stdout().flush();
							}
						}
					}
					
					buffer.clear();
				}
				
				if let Ok(pos) = reader.seek(SeekFrom::Current(0)) 
				{
					last_position = pos;
				}
			},
			Err(e) => 
			{
				println!("Error opening log file: {}", e);
			}
		}
		
		sleep(Duration::from_millis(500));
	}
}

fn is_unturned_running() -> bool 
{
	let output = Command::new("tasklist")
		.args(&["/FI", "IMAGENAME eq Unturned.exe", "/NH"])
		.output();
	
	match output 
	{
		Ok(output) => 
		{
			let output_str = String::from_utf8_lossy(&output.stdout);
			output_str.contains("Unturned.exe")
		},
		Err(_) => false
	}
}

fn directory_exists(path: &Path) -> bool
{
	path.exists() && path.is_dir()
}

fn file_exists(path: &Path) -> bool
{
	path.exists() && path.is_file()
}

fn copy_dir(src: &Path, dst: &Path) -> io::Result<()>
{
	fs::create_dir_all(dst)?;

	for entry in fs::read_dir(src)?
	{
		let entry = entry?;
		let src_path = entry.path();
		let dst_path = dst.join(entry.file_name());

		if src_path.is_dir()
		{
			copy_dir(&src_path, &dst_path)?;
		}
		else
		{
			fs::copy(&src_path, &dst_path)?;
		}
	}

	Ok(())
}

fn is_admin() -> bool
{
	unsafe
	{
		let process_handle = GetCurrentProcess();
		let mut token_handle = windows::Win32::Foundation::HANDLE::default();
		
		let token_result = OpenProcessToken(
			process_handle,
			TOKEN_QUERY,
			&mut token_handle
		);
		
		if !token_result.as_bool() 
		{
			return false;
		}
		
		let mut elevation = TOKEN_ELEVATION::default();
		let mut size = std::mem::size_of::<TOKEN_ELEVATION>() as u32;
		
		let result = GetTokenInformation(
			token_handle,
			TokenElevation,
			Some(&mut elevation as *mut _ as *mut _),
			size,
			&mut size
		);
		
		CloseHandle(token_handle);
		
		if !result.as_bool() 
		{
			return false;
		}
		
		elevation.TokenIsElevated != 0
	}
}

const CACHE_FILE: &str = ".gamepath";
const REGEX_PATTERN: &str = r#""path"\s*"([^"]+)"#;
const ALLOW_PATH_CACHE: bool = true;

fn get_steam_path_from_registry() -> Result<String, String>
{
	let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
	
	let registry_keys = [
		r"SOFTWARE\WOW6432Node\Valve\Steam",
		r"SOFTWARE\Valve\Steam",
	];
	
	for key in &registry_keys
	{
		match hklm.open_subkey(key)
		{
			Ok(subkey) => 
			{
				match subkey.get_value("InstallPath")
				{
					Ok(steam_path) => return Ok(steam_path),
					Err(_) => continue,
				}
			},
			Err(_) => continue,
		}
	}
	
	Err("Steam path not found in registry".to_string())
}

fn parse_library_folders(base_steam_path: &str) -> Vec<String>
{
	let mut library_paths = vec![base_steam_path.to_string()];
	let library_folders_file = Path::new(base_steam_path)
		.join("steamapps")
		.join("libraryfolders.vdf");
	
	if !file_exists(&library_folders_file)
	{
		return library_paths;
	}
	
	let content = match fs::read_to_string(&library_folders_file)
	{
		Ok(content) => content,
		Err(e) => 
		{
			println!("Error reading library folders: {}", e);
			return library_paths;
		}
	};
	
	let re = Regex::new(REGEX_PATTERN).unwrap();
	
	for cap in re.captures_iter(&content)
	{
		if let Some(m) = cap.get(1)
		{
			let library_path = m.as_str().replace("\\\\", "\\");
			let common_path = Path::new(&library_path)
				.join("steamapps")
				.join("common");
			
			if directory_exists(&common_path)
			{
				library_paths.push(library_path);
			}
		}
	}
	
	library_paths
}

fn find_game_directory(game: &str) -> Result<PathBuf, String>
{
	let steam_path = get_steam_path_from_registry()?;
	let library_paths = parse_library_folders(&steam_path);
	
	for library_path in library_paths
	{
		let common_path = Path::new(&library_path)
			.join("steamapps")
			.join("common");
		
		if !directory_exists(&common_path)
		{
			continue;
		}
		
		match fs::read_dir(&common_path)
		{
			Ok(entries) => 
			{
				for entry in entries
				{
					match entry
					{
						Ok(entry) => 
						{
							if entry.file_name() == game
							{
								let game_path = common_path.join(game);
								let exe_path = game_path.join(format!("{}.exe", game));
								
								if file_exists(&exe_path)
								{
									return Ok(game_path);
								}
							}
						},
						Err(e) => 
						{
							println!("Failed to access entry: {}", e);
							continue;
						}
					}
				}
			},
			Err(e) => 
			{
				println!("Failed to access directory {}: {}", common_path.display(), e);
				continue;
			}
		}
	}
	
	Err(format!("Game directory for {} not found", game))
}

fn write_cache_path(path: &Path) -> bool
{
	if !ALLOW_PATH_CACHE
	{
		return false;
	}
	
	match fs::write(CACHE_FILE, path.to_string_lossy().as_bytes())
	{
		Ok(_) => true,
		Err(e) => 
		{
			println!("Error writing cache: {}", e);
			false
		}
	}
}

fn read_cache_path() -> Option<PathBuf>
{
	if !ALLOW_PATH_CACHE
	{
		return None;
	}
	
	match fs::read_to_string(CACHE_FILE)
	{
		Ok(content) => Some(PathBuf::from(content.trim())),
		Err(_) => None,
	}
}

fn find_unturned_game_path() -> Result<PathBuf, String>
{
	const GAME_NAME: &str = "Unturned";
	
	if ALLOW_PATH_CACHE
	{
		if let Some(path) = read_cache_path()
		{
			if directory_exists(&path)
			{
				return Ok(path);
			}
		}
	}
	
	match find_game_directory(GAME_NAME)
	{
		Ok(path) => 
		{
			if ALLOW_PATH_CACHE
			{
				write_cache_path(&path);
			}
			Ok(path)
		},
		Err(e) => Err(e),
	}
}
